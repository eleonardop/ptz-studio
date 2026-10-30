using System.Collections.Concurrent;
using System.Net.Sockets;
using PtzStudio.Core.Models;

namespace PtzStudio.Core.Visca;

public enum ConnectionState { Desconectado, Conectando, Conectado, ErroDeComunicacao }

public sealed record LogEntry(DateTime Time, string Kind, string Text)
{
    public override string ToString() => $"{Time:HH:mm:ss.fff} [{Kind}] {Text}";
}

public interface IViscaSender
{
    void Send(byte[] payload);
}

/// <summary>Cliente VISCA sobre UDP: envia comandos, recebe respostas, monitora a conexão e registra tudo.</summary>
public sealed class ViscaClient : IViscaSender, IDisposable
{
    private readonly object _gate = new();
    private readonly ConcurrentQueue<LogEntry> _log = new();
    private UdpClient? _udp;
    private CameraConfig _cfg = new();
    private CancellationTokenSource? _cts;
    private uint _seq;
    private DateTime _probeSentUtc;
    private TaskCompletionSource<bool>? _probe;
    private int _failures;
    private readonly SemaphoreSlim _probeGate = new(1, 1);

    public ConnectionState State { get; private set; } = ConnectionState.Desconectado;
    public string StateDetail { get; private set; } = "Sem conexão";
    public double LastRoundTripMs { get; private set; } = -1;
    public event Action<ConnectionState, string>? StateChanged;
    public event Action<LogEntry>? LogAdded;
    public event Action<ViscaResponse>? ResponseReceived;
    public IReadOnlyCollection<LogEntry> RecentLog => _log.ToArray();

    public void Connect(CameraConfig cfg)
    {
        lock (_gate)
        {
            Close();
            _cfg = cfg.Clone();
            _failures = 0;
            SetState(ConnectionState.Conectando, $"Conectando a {cfg.IpAddress}:{cfg.Port}...");
            try
            {
                OpenSocket();
            }
            catch (Exception e)
            {
                AddLog("ERRO", "Falha ao abrir socket: " + e.Message);
                SetState(ConnectionState.ErroDeComunicacao, e.Message);
                return;
            }
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _ = Task.Run(() => HealthLoop(ct));
        }
    }

    public void Disconnect()
    {
        lock (_gate) Close();
        SetState(ConnectionState.Desconectado, "Desconectado"); // fora do lock: o handler pode chamar o controlador (evita deadlock)
    }

    private void OpenSocket()
    {
        _udp = new UdpClient();
        // Windows: sem isto, um ICMP "porta inalcançável" faz o PRÓXIMO envio/recepção falhar (SIO_UDP_CONNRESET).
        try { _udp.Client.IOControl(-1744830452, new byte[4], null); } catch { }
        _udp.Connect(_cfg.IpAddress, _cfg.Port);
        var udp = _udp;
        var ct = _cts?.Token ?? CancellationToken.None;
        _ = Task.Run(() => ReceiveLoop(udp));
    }

    private void Close()
    {
        try { _cts?.Cancel(); } catch { }
        try { _udp?.Close(); } catch { }
        _udp = null;
        _cts = null;
    }

    public void Send(byte[] payload) => SendRaw(payload, ViscaPayloadType.Command);

    private void SendRaw(byte[] payload, ViscaPayloadType type)
    {
        UdpClient? udp;
        byte[] data;
        lock (_gate)
        {
            udp = _udp;
            if (udp == null) return;
            data = _cfg.Transport == ViscaTransportMode.ViscaOverIp ? ViscaFraming.Wrap(payload, _seq++, type) : payload;
        }
        try
        {
            udp.Send(data, data.Length);
            AddLog("TX", BitConverter.ToString(data).Replace("-", " "));
        }
        catch (Exception e)
        {
            AddLog("ERRO", "Envio falhou: " + e.Message);
            SetState(ConnectionState.ErroDeComunicacao, e.Message);
        }
    }

    private async Task ReceiveLoop(UdpClient udp)
    {
        while (true)
        {
            try
            {
                var r = await udp.ReceiveAsync().ConfigureAwait(false);
                var resp = ViscaResponse.Parse(r.Buffer, _cfg.Transport);
                AddLog("RX", $"{resp.Kind}: {resp.Hex}");
                var probe = _probe;
                if (probe != null)
                {
                    LastRoundTripMs = (DateTime.UtcNow - _probeSentUtc).TotalMilliseconds;
                    probe.TrySetResult(true);
                }
                _failures = 0;
                if (State != ConnectionState.Conectado) SetState(ConnectionState.Conectado, "Câmera respondendo");
                ResponseReceived?.Invoke(resp);
            }
            catch (ObjectDisposedException) { return; }
            catch (SocketException e)
            {
                // Em UDP, "porta inalcançável" aparece aqui quando o destino recusa o pacote.
                AddLog("ERRO", "Recepção: " + e.Message);
                await Task.Delay(500).ConfigureAwait(false);
                if (udp != _udp) return;
            }
            catch (Exception e)
            {
                AddLog("ERRO", "Recepção: " + e.Message);
                return;
            }
        }
    }

    /// <summary>Envia uma consulta de versão e espera qualquer resposta dentro do timeout configurado.</summary>
    public async Task<bool> TestConnectionAsync()
    {
        if (_udp == null) return false;
        await _probeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _probe = tcs;
            _probeSentUtc = DateTime.UtcNow;
            SendRaw(ViscaCommands.VersionInquiry(_cfg.ViscaAddress), ViscaPayloadType.Inquiry);
            var done = await Task.WhenAny(tcs.Task, Task.Delay(_cfg.TimeoutMs)).ConfigureAwait(false);
            _probe = null;
            bool ok = done == tcs.Task && tcs.Task.Result;
            if (!ok) LastRoundTripMs = -1;
            return ok;
        }
        finally { _probeGate.Release(); }
    }

    private async Task HealthLoop(CancellationToken ct)
    {
        try { await Task.Delay(1500, ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_cfg.HealthCheck)
                {
                    bool ok;
                    lock (_gate) { if (_udp == null) { try { OpenSocket(); } catch { } } }
                    ok = await TestConnectionAsync().ConfigureAwait(false);
                    if (!ok)
                    {
                        _failures++;
                        if (_failures >= Math.Max(1, _cfg.ReconnectAttempts))
                            SetState(ConnectionState.Desconectado, "Sem resposta da câmera (tentando reconectar...)");
                        else
                            SetState(ConnectionState.ErroDeComunicacao, $"Sem resposta ({_failures}/{_cfg.ReconnectAttempts})");
                    }
                }
                await Task.Delay(3000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { AddLog("ERRO", "Monitor: " + e.Message); }
        }
    }

    private void SetState(ConnectionState s, string detail)
    {
        bool changed = s != State;
        State = s;
        StateDetail = detail;
        if (changed)
        {
            AddLog("ESTADO", $"{s}: {detail}");
            StateChanged?.Invoke(s, detail);
        }
    }

    private void AddLog(string kind, string text)
    {
        var e = new LogEntry(DateTime.Now, kind, text);
        _log.Enqueue(e);
        while (_log.Count > 500) _log.TryDequeue(out _);
        LogAdded?.Invoke(e);
    }

    public void Dispose() { lock (_gate) Close(); }
}
