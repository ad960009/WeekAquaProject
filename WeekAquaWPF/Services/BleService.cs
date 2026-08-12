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

        private readonly ConcurrentQueue<byte[]> _writeQueue = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim _queueSemaphore = new SemaphoreSlim(0);
        private CancellationTokenSource? _queueCts;
        private Task? _queueTask;

        public event Action<BleDeviceInfo>? DeviceDiscovered;
        public event Action<bool, string>? ConnectionStateChanged;
        public event Action<byte[]>? DataReceived;
        public event Action<LogDirection, string, byte[]?>? LogMessage;

        public bool IsConnected => _currentDevice != null && _currentDevice.ConnectionStatus == BluetoothConnectionStatus.Connected && _writeCharacteristic != null;
        public string ConnectedDeviceName => _currentDevice?.Name ?? string.Empty;

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

                // Reverse-Engineered Android Model Code Check (5748, 5749, 574A)
                if (hex.Contains("5748") || hex.Contains("5749") || hex.Contains("574A"))
                {
                    hasUvChannel = true;
                    modelCode = hex.Contains("5748") ? "5748" : (hex.Contains("5749") ? "5749" : "574A");
                }
                else if (hex.Contains("5746"))
                {
                    modelCode = "5746";
                }
                else if (hex.Contains("5747"))
                {
                    modelCode = "5747";
                }
            }
            catch { }

            // Name fallback check
            if (!hasUvChannel && !string.IsNullOrWhiteSpace(name))
            {
                string upperName = name.ToUpperInvariant();
                if (upperName.Contains("UV") || upperName.Contains("UVA") || upperName.Contains("_M90") || upperName.Contains("_T90"))
                {
                    hasUvChannel = true;
                }
            }

            var deviceInfo = new BleDeviceInfo
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Unknown BLE Device" : name,
                BluetoothAddress = args.BluetoothAddress,
                MacAddress = macStr,
                Rssi = args.RawSignalStrengthInDBm,
                ModelCode = modelCode,
                HasUvChannel = hasUvChannel
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

                // Step 4 of Handshake: Send initial RTC Sync packet
                EnqueueWritePacket(WeekAquaProtocol.BuildRtcSyncPacket(DateTime.Now));

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
        public void EnqueueWritePacket(byte[] packetData)
        {
            if (packetData == null || packetData.Length == 0) return;
            _writeQueue.Enqueue(packetData);
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
                        if (_writeQueue.TryDequeue(out byte[]? packetData))
                        {
                            if (IsConnected && _writeCharacteristic != null && packetData != null)
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
                                        LogMessage?.Invoke(LogDirection.TX, $"Sent {packetData.Length} bytes", packetData);
                                    }
                                    else
                                    {
                                        LogMessage?.Invoke(LogDirection.Error, $"Write failed: {status.Status}", packetData);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogMessage?.Invoke(LogDirection.Error, $"Write exception: {ex.Message}", packetData);
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
