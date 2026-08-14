using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using WeekAquaWPF.Models;
using WeekAquaWPF.Protocol;

namespace WeekAquaWPF.Services
{
    public class BleService : IDisposable
    {
        private BluetoothLEAdvertisementWatcher? _watcher;
        private BluetoothLEDevice? _currentDevice;
        private GattCharacteristic? _writeCharacteristic;
        private GattCharacteristic? _notifyCharacteristic;

        private readonly ConcurrentQueue<(byte[] Data, string? Description)> _writeQueue = new ConcurrentQueue<(byte[], string?)>();
        private readonly SemaphoreSlim _queueSemaphore = new SemaphoreSlim(0);
        private CancellationTokenSource? _queueCts;
        private Task? _queueTask;

        public event Action<BleDeviceInfo>? DeviceDiscovered;
        public event Action<bool, string>? ConnectionStateChanged;
        public event Action<byte[]>? DataReceived;
        public event Action<LogDirection, string, byte[]?>? LogMessage;

        private bool _isVirtualConnected = false;

        public bool IsConnected => _isVirtualConnected || (_currentDevice != null && _currentDevice.ConnectionStatus == BluetoothConnectionStatus.Connected && _writeCharacteristic != null);
        public string ConnectedDeviceName => _isVirtualConnected ? "Virtual Device" : (_currentDevice?.Name ?? string.Empty);

        public BleService()
        {
            StartQueueProcessor();
        }

        public void StartScan()
        {
            if (_watcher != null)
            {
                StopScan();
            }

            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            _watcher.Received += OnAdvertisementReceived;
            _watcher.Start();

            LogMessage?.Invoke(LogDirection.Info, "BLE Scanning started...", null);
        }

        public void StopScan()
        {
            if (_watcher != null)
            {
                _watcher.Stop();
                _watcher.Received -= OnAdvertisementReceived;
                _watcher = null;
                LogMessage?.Invoke(LogDirection.Info, "BLE Scanning stopped.", null);
            }
        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            string name = args.Advertisement.LocalName;
            string macStr = FormatMacAddress(args.BluetoothAddress);

            string modelCode = string.Empty;
            bool hasUvChannel = false;

            try
            {
                var rawBytes = new List<byte>();
                foreach (var section in args.Advertisement.DataSections)
                {
                    var reader = Windows.Storage.Streams.DataReader.FromBuffer(section.Data);
                    byte[] bytes = new byte[section.Data.Length];
                    reader.ReadBytes(bytes);
                    rawBytes.AddRange(bytes);
                }

                string hex = BitConverter.ToString(rawBytes.ToArray()).Replace("-", "").ToUpperInvariant();

                // Reverse-Engineered Official Android Model Code Check (5745 ~ 5752)
                string[] targetCodes = new[] { "5752", "5751", "5750", "5749", "5748", "5747", "5746", "5745" };
                foreach (var code in targetCodes)
                {
                    if (hex.Contains(code))
                    {
                        modelCode = code;
                        break;
                    }
                }

                if (modelCode == "5748" || modelCode == "5749" || modelCode == "5750" || modelCode == "5751" || modelCode == "5752")
                {
                    hasUvChannel = true;
                }
            }
            catch { }

            bool isKnownWeekAqua = false;

            // 1. Model code detected from BLE Advertisement Hex
            if (!string.IsNullOrEmpty(modelCode))
            {
                isKnownWeekAqua = true;
            }

            // 2. Service UUID Check (FFE0, FFF0, FF60, FEE0)
            if (!isKnownWeekAqua && args.Advertisement.ServiceUuids != null)
            {
                foreach (var uuid in args.Advertisement.ServiceUuids)
                {
                    string uStr = uuid.ToString().ToUpperInvariant();
                    if (uStr.Contains("FFE0") || uStr.Contains("FFF0") || uStr.Contains("FF60") || uStr.Contains("FEE0"))
                    {
                        isKnownWeekAqua = true;
                        break;
                    }
                }
            }

            bool is4ChannelRgbUv = false;

            // 3. Name check & 4-Channel RGB/UV vs 5-Channel UV distinction
            if (!string.IsNullOrWhiteSpace(name))
            {
                string upperName = name.ToUpperInvariant();

                if (upperName.Contains("WEEK") || upperName.Contains("AQUA") || upperName.Contains("LIGHT") || upperName.Contains("LAMP") ||
                    upperName.Contains("PLUG") || upperName.Contains("SOCKET") || upperName.Contains("SP0") ||
                    upperName.Contains("M-") || upperName.Contains("S-") || upperName.Contains("T-") || upperName.Contains("A-") || upperName.Contains("L-") ||
                    upperName.Contains("T90") || upperName.Contains("T60") || upperName.Contains("T80") || upperName.Contains("T120") || upperName.Contains("T45") ||
                    upperName.Contains("M450") || upperName.Contains("M600") || upperName.Contains("M800") || upperName.Contains("M900") || upperName.Contains("M1200") ||
                    upperName.Contains("S450") || upperName.Contains("S600") || upperName.Contains("S800") || upperName.Contains("S900") || upperName.Contains("S1200") ||
                    upperName.Contains("CORAL") || upperName.Contains("MARINE") ||
                    System.Text.RegularExpressions.Regex.IsMatch(upperName, @"\b[MSTZPAL][0-9]{2,4}"))
                {
                    isKnownWeekAqua = true;
                }

                bool isMultiChannel = modelCode == "5748" || modelCode == "5749" || modelCode == "5750" || modelCode == "5751" || modelCode == "5752" ||
                                      upperName.Contains("5CH") || upperName.Contains("6CH") || upperName.Contains("10CH") || upperName.Contains("MARINE") || upperName.Contains("CORAL") || upperName.Contains("A-SERIES") || upperName.Contains("A430");

                if (!isMultiChannel)
                {
                    bool isRgbUv = upperName.Contains("UV") || upperName.Contains("UVA") || upperName.Contains("RGB/UV") || upperName.Contains("RGB-UV") || upperName.Contains("RGB_UV") ||
                                   upperName.Contains("M800") || upperName.Contains("M600") || upperName.Contains("M450") || upperName.Contains("M400") || upperName.Contains("M900") || upperName.Contains("M1200") || upperName.Contains("M-PRO") || upperName.Contains("M PRO") || upperName.Contains("M_PRO") || upperName.StartsWith("M") ||
                                   upperName.Contains("S400") || upperName.Contains("S450") || upperName.Contains("S600") || upperName.Contains("S800") || upperName.Contains("S900") || upperName.Contains("S1200") || upperName.Contains("S-PRO") || upperName.Contains("S PRO") || upperName.Contains("S_PRO") ||
                                   upperName.Contains("T90") || upperName.Contains("T45") || upperName.Contains("T60") || upperName.Contains("T80") || upperName.Contains("T120") ||
                                   upperName.Contains("Z400") || upperName.Contains("Z600") || upperName.Contains("Z800") ||
                                   upperName.Contains("P600") || upperName.Contains("P800") || upperName.Contains("P900") || upperName.Contains("P1200") ||
                                   System.Text.RegularExpressions.Regex.IsMatch(upperName, @"\b[MSTZP][0-9]{2,4}");

                    if (isRgbUv)
                    {
                        is4ChannelRgbUv = true;
                        hasUvChannel = false;
                        if (string.IsNullOrEmpty(modelCode)) modelCode = "5746";
                    }
                }
            }

            // If not recognized as a WeekAqua device or compatible lighting hardware, ignore and skip
            if (!isKnownWeekAqua)
            {
                return;
            }

            var deviceInfo = new BleDeviceInfo
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"WeekAqua Device ({modelCode})" : name,
                BluetoothAddress = args.BluetoothAddress,
                MacAddress = macStr,
                Rssi = args.RawSignalStrengthInDBm,
                ModelCode = modelCode,
                HasUvChannel = hasUvChannel,
                Is4ChannelRgbUv = is4ChannelRgbUv
            };

            DeviceDiscovered?.Invoke(deviceInfo);
        }

        public async Task<bool> ConnectAsync(ulong bluetoothAddress)
        {
            try
            {
                StopScan();
                Disconnect();

                LogMessage?.Invoke(LogDirection.Info, $"Connecting to BLE address {FormatMacAddress(bluetoothAddress)}...", null);

                if (bluetoothAddress >= 0xAABBCC112233 && bluetoothAddress <= 0xAABBCC556677)
                {
                    _isVirtualConnected = true;
                    LogMessage?.Invoke(LogDirection.Info, $"Connected to Virtual Demo Device ({FormatMacAddress(bluetoothAddress)}).", null);
                    ConnectionStateChanged?.Invoke(true, "Connected (Virtual Mode)");
                    return true;
                }

                _currentDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                if (_currentDevice == null)
                {
                    LogMessage?.Invoke(LogDirection.Error, "Failed to connect: Device unreachable.", null);
                    ConnectionStateChanged?.Invoke(false, "Device unreachable.");
                    return false;
                }

                _currentDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

                // MTU Request (Attempting 128 as specified in protocol)
                try
                {
                    var gattSession = await GattSession.FromDeviceIdAsync(_currentDevice.BluetoothDeviceId);
                    if (gattSession != null)
                    {
                        gattSession.MaxPduSizeChanged += (s, e) =>
                        {
                            LogMessage?.Invoke(LogDirection.Info, $"MTU updated to: {s.MaxPduSize}", null);
                        };
                    }
                }
                catch { /* Ignore MTU request unsupported on some adapters */ }

                // Discover GATT Services
                var servicesResult = await _currentDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    LogMessage?.Invoke(LogDirection.Error, $"GATT Service discovery failed: {servicesResult.Status}", null);
                    ConnectionStateChanged?.Invoke(false, "GATT Service discovery failed.");
                    return false;
                }

                // Check FFE0 series or FFF0 series
                GattDeviceService? targetService = null;
                Guid targetWriteUuid = Guid.Empty;
                Guid targetNotifyUuid = Guid.Empty;

                foreach (var service in servicesResult.Services)
                {
                    if (service.Uuid == WeekAquaProtocol.SERVICE_FFE0)
                    {
                        targetService = service;
                        targetWriteUuid = WeekAquaProtocol.WRITE_FFE1;
                        targetNotifyUuid = WeekAquaProtocol.NOTIFY_FFE3;
                        LogMessage?.Invoke(LogDirection.Info, "Detected FFE0 Primary Service series.", null);
                        break;
                    }
                    else if (service.Uuid == WeekAquaProtocol.SERVICE_FFF0)
                    {
                        targetService = service;
                        targetWriteUuid = WeekAquaProtocol.WRITE_FFF2;
                        targetNotifyUuid = WeekAquaProtocol.NOTIFY_FFF1;
                        LogMessage?.Invoke(LogDirection.Info, "Detected FFF0 Primary Service series.", null);
                        break;
                    }
                }

                if (targetService == null)
                {
                    // Fallback search inside services
                    foreach (var service in servicesResult.Services)
                    {
                        var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        if (chars.Status == GattCommunicationStatus.Success)
                        {
                            foreach (var c in chars.Characteristics)
                            {
                                if (c.Uuid == WeekAquaProtocol.WRITE_FFE1 || c.Uuid == WeekAquaProtocol.WRITE_FFF2)
                                {
                                    _writeCharacteristic = c;
                                }
                                if (c.Uuid == WeekAquaProtocol.NOTIFY_FFE3 || c.Uuid == WeekAquaProtocol.NOTIFY_FFF1)
                                {
                                    _notifyCharacteristic = c;
                                }
                            }
                        }
                    }
                }
                else
                {
                    var charsResult = await targetService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (charsResult.Status == GattCommunicationStatus.Success)
                    {
                        _writeCharacteristic = charsResult.Characteristics.FirstOrDefault(c => c.Uuid == targetWriteUuid);
                        _notifyCharacteristic = charsResult.Characteristics.FirstOrDefault(c => c.Uuid == targetNotifyUuid);
                    }
                }

                if (_writeCharacteristic == null)
                {
                    LogMessage?.Invoke(LogDirection.Error, "Write Characteristic not found on device.", null);
                    ConnectionStateChanged?.Invoke(false, "Write Characteristic missing.");
                    return false;
                }

                // Subscribe to Notifications if available
                if (_notifyCharacteristic != null)
                {
                    try
                    {
                        var cccdResult = await _notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.Notify);

                        if (cccdResult == GattCommunicationStatus.Success)
                        {
                            _notifyCharacteristic.ValueChanged += OnNotificationReceived;
                            LogMessage?.Invoke(LogDirection.Info, "Subscribed to Notify characteristic.", null);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke(LogDirection.Error, $"Failed to enable notifications: {ex.Message}", null);
                    }
                }

                LogMessage?.Invoke(LogDirection.Info, $"Successfully connected to {_currentDevice.Name}", null);
                ConnectionStateChanged?.Invoke(true, "Connected");

                // Handshake Step 4: Send initial RTC Sync packet with BCD time
                EnqueueWritePacket(WeekAquaProtocol.BuildRtcSyncPacket(DateTime.Now), "Initial RTC Sync");

                // Handshake Step 5: Send state initialization packet (0xF0)
                EnqueueWritePacket(WeekAquaProtocol.BuildStateInitPacket(), "State Reset (F0)");

                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(LogDirection.Error, $"Connection error: {ex.Message}", null);
                ConnectionStateChanged?.Invoke(false, ex.Message);
                return false;
            }
        }

        private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            bool connected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;
            LogMessage?.Invoke(LogDirection.Info, $"Connection status changed: {sender.ConnectionStatus}", null);
            ConnectionStateChanged?.Invoke(connected, connected ? "Connected" : "Disconnected");
        }

        private void OnNotificationReceived(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] data = args.CharacteristicValue.ToArray();
            LogMessage?.Invoke(LogDirection.RX, $"Received {data.Length} bytes", data);
            DataReceived?.Invoke(data);
        }

        /// <summary>
        /// Enqueues a packet to be written with a 500ms delay between consecutive packets (Protocol requirement).
        /// </summary>
        public void EnqueueWritePacket(byte[] packetData, string? description = null)
        {
            if (packetData == null || packetData.Length == 0) return;
            _writeQueue.Enqueue((packetData, description));
            _queueSemaphore.Release();
        }

        private void StartQueueProcessor()
        {
            _queueCts = new CancellationTokenSource();
            _queueTask = Task.Run(async () =>
            {
                while (!_queueCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await _queueSemaphore.WaitAsync(_queueCts.Token);
                        if (_writeQueue.TryDequeue(out var item))
                        {
                            var packetData = item.Data;
                            var desc = item.Description;
                            string txLogText = !string.IsNullOrEmpty(desc) ? desc : $"Sent {packetData.Length} bytes";

                            if (_isVirtualConnected && packetData != null)
                            {
                                LogMessage?.Invoke(LogDirection.TX, $"[Virtual] {txLogText}", packetData);
                                await Task.Delay(500, _queueCts.Token);
                            }
                            else if (IsConnected && _writeCharacteristic != null && packetData != null)
                            {
                                try
                                {
                                    using var writer = new DataWriter();
                                    writer.WriteBytes(packetData);
                                    IBuffer buffer = writer.DetachBuffer();

                                    var writeOption = _writeCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
                                        ? GattWriteOption.WriteWithoutResponse
                                        : GattWriteOption.WriteWithResponse;

                                    var status = await _writeCharacteristic.WriteValueWithResultAsync(buffer, writeOption);
                                    if (status.Status == GattCommunicationStatus.Success)
                                    {
                                        LogMessage?.Invoke(LogDirection.TX, txLogText, packetData);
                                    }
                                    else
                                    {
                                        LogMessage?.Invoke(LogDirection.Error, $"Write failed: {status.Status} ({txLogText})", packetData);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogMessage?.Invoke(LogDirection.Error, $"Write exception: {ex.Message} ({txLogText})", packetData);
                                }

                                // Protocol Requirement: 500ms delay between write packets to prevent packet drop
                                await Task.Delay(500, _queueCts.Token);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke(LogDirection.Error, $"Queue error: {ex.Message}", null);
                    }
                }
            }, _queueCts.Token);
        }

        public void Disconnect()
        {
            _isVirtualConnected = false;
            if (_notifyCharacteristic != null)
            {
                _notifyCharacteristic.ValueChanged -= OnNotificationReceived;
                _notifyCharacteristic = null;
            }

            _writeCharacteristic = null;

            if (_currentDevice != null)
            {
                _currentDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _currentDevice.Dispose();
                _currentDevice = null;
                ConnectionStateChanged?.Invoke(false, "Disconnected");
            }
        }

        private static string FormatMacAddress(ulong address)
        {
            string hex = address.ToString("X12");
            return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
        }

        public void Dispose()
        {
            StopScan();
            Disconnect();
            _queueCts?.Cancel();
            _queueCts?.Dispose();
            _queueSemaphore?.Dispose();
        }
    }
}
