using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CLAWDESK.Models;

namespace CLAWDESK.Services
{
    public class OpenClawService
    {
        private ClientWebSocket? _webSocket;
        private readonly ConfigService _configService;
        private string? _sessionKey;
        private string? _authToken;
        private bool _isConnected;
        private bool _isHandshakeComplete;

        // Pending requests for response handling
        private readonly Dictionary<string, (Action<JsonElement> resolve, Action<string> reject)> _pendingRequests = new();

        public OpenClawService(ConfigService configService)
        {
            _configService = configService;
        }

        public bool IsConnected => _isConnected && _isHandshakeComplete;

        public async Task<bool> ConnectAsync()
        {
            var config = _configService.GetConfig();
            _authToken = string.IsNullOrEmpty(config.ApiKey) ? GetGatewayToken() : config.ApiKey;

            System.Diagnostics.Debug.WriteLine($"[OpenClawService] ConnectAsync - GatewayUrl: {config.GatewayUrl}");

            // 嘗試 wss://，如果失敗則 fallback 到 ws://
            string[] protocols = { "wss://", "ws://" };
            
            foreach (var protocol in protocols)
            {
                try
                {
                    var url = config.GatewayUrl.Replace("http://", protocol).Replace("https://", protocol);
                    System.Diagnostics.Debug.WriteLine($"[OpenClawService] Connecting to: {url}");
                    
                    _webSocket = new ClientWebSocket();
                    _webSocket.Options.SetRequestHeader("Origin", "http://localhost:18789");
                    
                    await _webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
                    _isConnected = true;

                    // Start receiving messages in a background task
                    _ = ReceiveMessagesAsync();

                    // Wait for handshake to complete (will be set by handleMessage)
                    int timeout = 15000;
                    int elapsed = 0;
                    while (!_isHandshakeComplete && elapsed < timeout)
                    {
                        await Task.Delay(100);
                        elapsed += 100;
                    }

                    if (!_isHandshakeComplete)
                    {
                        System.Diagnostics.Debug.WriteLine("[OpenClawService] Handshake timeout");
                        _webSocket?.Dispose();
                        _webSocket = null;
                        continue; // Try next protocol
                    }

                    System.Diagnostics.Debug.WriteLine($"[OpenClawService] Connected! SessionKey: {_sessionKey}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenClawService] Connect error with {protocol}: {ex.Message}");
                    _webSocket?.Dispose();
                    _webSocket = null;
                    _isConnected = false;
                    continue; // Try next protocol
                }
            }

            System.Diagnostics.Debug.WriteLine("[OpenClawService] All connection attempts failed");
            return false;
        }

        private StringBuilder _receiveBuffer = new StringBuilder();  // 累積收到的資料
        
        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[65536];
            
            try
            {
                while (_webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _isConnected = false;
                        _isHandshakeComplete = false;
                        break;
                    }

                    // 將收到的資料累積到 buffer
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _receiveBuffer.Append(json);
                    
                    // 嘗試解析完整的 JSON 物件
                    ProcessReceiveBuffer();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenClawService] Receive error: {ex.Message}");
                _isConnected = false;
                _isHandshakeComplete = false;
            }
        }
        
        private void ProcessReceiveBuffer()
        {
            // 簡單的處理：嘗試找到完整的 JSON 物件（大括號配對）
            var buffer = _receiveBuffer.ToString();
            var depth = 0;
            var startIndex = -1;
            
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == '{')
                {
                    if (depth == 0) startIndex = i;
                    depth++;
                }
                else if (buffer[i] == '}')
                {
                    depth--;
                    if (depth == 0 && startIndex >= 0)
                    {
                        // 找到一個完整的 JSON 物件
                        var json = buffer.Substring(startIndex, i - startIndex + 1);
                        try
                        {
                            HandleMessage(json);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[OpenClawService] Parse error: {ex.Message}");
                        }
                        startIndex = -1;
                    }
                }
            }
            
            // 保留未完成的資料
            if (startIndex >= 0 && startIndex < buffer.Length)
            {
                _receiveBuffer.Clear();
                _receiveBuffer.Append(buffer.Substring(startIndex));
            }
            else
            {
                _receiveBuffer.Clear();
            }
        }

        private void HandleMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check frame type
                if (!root.TryGetProperty("type", out var typeElement))
                    return;

                var frameType = typeElement.GetString();

                switch (frameType)
                {
                    case "event":
                        HandleEvent(root);
                        break;
                    case "res":
                        HandleResponse(root);
                        break;
                    case "req":
                        // Server-initiated request (rare)
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenClawService] HandleMessage error: {ex.Message}");
            }
        }

        private void HandleEvent(JsonElement frame)
        {
            if (!frame.TryGetProperty("event", out var eventElement))
                return;

            var eventName = eventElement.GetString();
            System.Diagnostics.Debug.WriteLine($"[OpenClawService] Event: {eventName}");

            switch (eventName)
            {
                case "connect.challenge":
                    // Server sent challenge - respond with connect handshake
                    SendConnectHandshake();
                    break;

                case "chat":
                case "chat.message":
                    // New chat message from agent - will be handled via callback
                    break;

                case "chat.stream":
                    // Streaming response
                    break;

                case "tick":
                    // Heartbeat - ignore
                    break;

                case "shutdown":
                    _isConnected = false;
                    _isHandshakeComplete = false;
                    break;
            }
        }

        private void HandleResponse(JsonElement frame)
        {
            if (!frame.TryGetProperty("id", out var idElement))
                return;

            var id = idElement.GetString();
            if (string.IsNullOrEmpty(id) || !_pendingRequests.ContainsKey(id))
                return;

            var (resolve, reject) = _pendingRequests[id];
            _pendingRequests.Remove(id);

            if (frame.TryGetProperty("ok", out var okElement) && okElement.GetBoolean())
            {
                if (frame.TryGetProperty("payload", out var payload))
                {
                    // Check for hello-ok (connection success)
                    if (payload.TryGetProperty("type", out var typeElement) && typeElement.GetString() == "hello-ok")
                    {
                        _isHandshakeComplete = true;
                        
                        // Extract session key from snapshot
                        if (payload.TryGetProperty("snapshot", out var snapshot))
                        {
                            if (snapshot.TryGetProperty("session", out var session))
                            {
                                if (session.TryGetProperty("mainSessionKey", out var sessionKey))
                                {
                                    _sessionKey = sessionKey.GetString();
                                }
                                else if (session.TryGetProperty("mainKey", out var mainKey))
                                {
                                    _sessionKey = mainKey.GetString();
                                }
                            }
                        }
                        
                        // Fallback session key
                        if (string.IsNullOrEmpty(_sessionKey))
                            _sessionKey = "main";

                        System.Diagnostics.Debug.WriteLine($"[OpenClawService] Handshake complete! SessionKey: {_sessionKey}");
                    }
                    
                    resolve(payload);
                }
                else
                {
                    resolve(new JsonElement());
                }
            }
            else
            {
                var errorMsg = "Unknown error";
                if (frame.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("message", out var msg))
                        errorMsg = msg.GetString() ?? errorMsg;
                    else
                        errorMsg = error.GetRawText();
                }
                reject(errorMsg);
            }
        }

        private void SendConnectHandshake()
        {
            var config = _configService.GetConfig();
            
            var connectFrame = new
            {
                type = "req",
                id = Guid.NewGuid().ToString("N"),
                method = "connect",
                @params = new
                {
                    minProtocol = 3,
                    maxProtocol = 3,
                    client = new
                    {
                        id = "openclaw-control-ui",
                        displayName = "CLAWDESK",
                        version = "1.0.0",
                        platform = "windows",
                        mode = "ui"
                    },
                    role = "operator",
                    scopes = new[] { "operator.read", "operator.write" },
                    auth = new
                    {
                        token = _authToken ?? ""
                    }
                }
            };

            // Register pending request
            var id = connectFrame.id;
            var tcs = new TaskCompletionSource<JsonElement>();
            _pendingRequests[id] = (
                resolve: (JsonElement p) => tcs.SetResult(p),
                reject: (string e) => tcs.SetException(new Exception(e))
            );

            SendJson(connectFrame);
            System.Diagnostics.Debug.WriteLine("[OpenClawService] Sent connect handshake");
        }

        public async Task<string> SendMessageAsync(string message)
        {
            // If not connected, try to connect
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                var connected = await ConnectAsync();
                if (!connected)
                {
                    return "[連線錯誤] 無法連接到 Gateway\n\n請確認 Gateway 是否正在執行。";
                }
            }

            // Use session key or default
            var sessionKey = string.IsNullOrEmpty(_sessionKey) ? "main" : _sessionKey;

            try
            {
                var requestId = Guid.NewGuid().ToString("N");
                
                var request = new
                {
                    type = "req",
                    id = requestId,
                    method = "chat.send",
                    @params = new
                    {
                        sessionKey = sessionKey,
                        message = message,
                        idempotencyKey = Guid.NewGuid().ToString("N")
                    }
                };

                // Register pending request
                var tcs = new TaskCompletionSource<string>();
                _pendingRequests[requestId] = (
                    resolve: (JsonElement p) => 
                    {
                        // Extract content from response
                        string? content = null;
                        if (p.TryGetProperty("content", out var contentProp))
                            content = contentProp.GetString();
                        if (string.IsNullOrEmpty(content) && p.TryGetProperty("text", out var textProp))
                            content = textProp.GetString();
                        if (string.IsNullOrEmpty(content))
                            content = p.GetRawText();
                        tcs.SetResult(content ?? "無回覆");
                    },
                    reject: (string e) => tcs.SetException(new Exception(e))
                );

                SendJson(request);
                System.Diagnostics.Debug.WriteLine($"[OpenClawService] Sent message: {message.Substring(0, Math.Min(50, message.Length))}...");

                // Wait for response with timeout
                var timeoutCts = new CancellationTokenSource(30000);
                try
                {
                    return await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    _pendingRequests.Remove(requestId);
                    return "[錯誤] 請求逾時";
                }
            }
            catch (Exception ex)
            {
                return $"[錯誤] {ex.Message}";
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            return await ConnectAsync();
        }

        private string? GetGatewayToken()
        {
            try
            {
                var openClawPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".openclaw",
                    "openclaw.json");

                if (!System.IO.File.Exists(openClawPath))
                    return null;

                var json = System.IO.File.ReadAllText(openClawPath);
                using var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("gateway", out var gateway) &&
                    gateway.TryGetProperty("auth", out var auth) &&
                    auth.TryGetProperty("token", out var token))
                {
                    return token.GetString();
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }

        private void SendJson(object obj)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;

            var json = JsonSerializer.Serialize(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
        }

        public void Disconnect()
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait();
                }
                catch { }
            }
            _webSocket = null;
            _isConnected = false;
            _isHandshakeComplete = false;
            _sessionKey = null;
            _pendingRequests.Clear();
        }
    }
}