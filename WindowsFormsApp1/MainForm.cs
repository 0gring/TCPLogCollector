using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace TCPLogCollector
{
    public partial class MainForm : Form
    {
        private TcpListener tcpListener;
        private UdpClient udpClient;
        private CancellationTokenSource cts;
        private List<LogEntry> logEntries = new List<LogEntry>();
        private string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TCPLogs");
        private LogFormat selectedFormat = LogFormat.TXT;
        private ProtocolType selectedProtocol = ProtocolType.TCP;
        private const int PORT = 514;
        private PictureBox statusIndicator;
        private Label statusIndicatorLabel;

        // Auto-save related fields
        private bool autoSaveEnabled = true;
        private string autoSaveFileName;
        private StreamWriter autoSaveWriter;
        private int autoSaveInterval = 1; // Save every X logs
        private int logCountSinceLastSave = 0;
        private int maxLogFileSize = 10; // MB
        private readonly object fileLock = new object();

        public MainForm()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            // Set basic form properties
            this.Text = "Log Collector";
            this.Width = 700;
            this.Height = 590; // Increased height for auto-save controls
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create the main layout
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };

            mainLayout.RowStyles.Clear();
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));


            this.Controls.Add(mainLayout);

            // Configuration Group
            GroupBox configGroup = new GroupBox
            {
                Text = "Configuration",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Create configuration layout
            TableLayoutPanel configLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5, // Increased rows for auto-save settings
                ColumnCount = 2
            };

            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Save location controls
            Label saveLocationLabel = new Label
            {
                Text = "Save Location:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };

            Panel saveLocationPanel = new Panel { Dock = DockStyle.Fill };
            TextBox saveLocationTextBox = new TextBox
            {
                Text = savePath,
                Width = 390,
                Location = new System.Drawing.Point(0, 5)
            };

            Button browseButton = new Button
            {
                Text = "Browse",
                Width = 80,
                Location = new System.Drawing.Point(saveLocationTextBox.Width + 5, 3)
            };

            saveLocationPanel.Controls.Add(saveLocationTextBox);
            saveLocationPanel.Controls.Add(browseButton);

            // Format selection controls
            Label formatLabel = new Label
            {
                Text = "Log Format:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };

            Panel formatPanel = new Panel { Dock = DockStyle.Fill };

            ComboBox formatComboBox = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new System.Drawing.Point(0, 5)
            };

            formatComboBox.Items.AddRange(new object[] { "Text (.txt)", "CSV (.csv)", "JSON (.json)", "Syslog (.log)" });
            formatComboBox.SelectedIndex = 0;

            // Protocol selection controls
            Label protocolLabel = new Label
            {
                Text = "Protocol:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };

            Panel protocolPanel = new Panel { Dock = DockStyle.Fill };

            RadioButton tcpRadioButton = new RadioButton
            {
                Text = "TCP",
                Checked = true,
                AutoSize = true,
                Location = new System.Drawing.Point(0, 5)
            };

            RadioButton udpRadioButton = new RadioButton
            {
                Text = "UDP",
                AutoSize = true,
                Location = new System.Drawing.Point(60, 5)
            };

            // Port display
            Label portLabel = new Label
            {
                Text = $"Port: {PORT}",
                AutoSize = true,
                Location = new System.Drawing.Point(120, 5)
            };

            formatPanel.Controls.Add(formatComboBox);

            protocolPanel.Controls.Add(tcpRadioButton);
            protocolPanel.Controls.Add(udpRadioButton);
            protocolPanel.Controls.Add(portLabel);

            // Auto-save controls
            Label autoSaveLabel = new Label
            {
                Text = "Auto-Save:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };

            Panel autoSavePanel = new Panel { Dock = DockStyle.Fill };

            CheckBox autoSaveCheckBox = new CheckBox
            {
                Text = "Enable Auto-Save",
                Checked = autoSaveEnabled,
                AutoSize = true,
                Location = new System.Drawing.Point(0, 5)
            };

            Label maxFileSizeLabel = new Label
            {
                Text = "Max File Size (MB):",
                AutoSize = true,
                Location = new System.Drawing.Point(145, 5)
            };

            NumericUpDown maxFileSizeUpDown = new NumericUpDown
            {
                Value = maxLogFileSize,
                Minimum = 1,
                Maximum = 10000,
                Width = 60,
                Location = new System.Drawing.Point(250, 3)
            };

            autoSavePanel.Controls.Add(autoSaveCheckBox);
            autoSavePanel.Controls.Add(maxFileSizeLabel);
            autoSavePanel.Controls.Add(maxFileSizeUpDown);

            // Add auto-save file name controls
            Label autoSaveFileNameLabel = new Label
            {
                Text = "Auto-Save File:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };

            Panel autoSaveFileNamePanel = new Panel { Dock = DockStyle.Fill };

            TextBox autoSaveFileNameTextBox = new TextBox
            {
                Text = "AutoSave",
                Width = 200,
                Location = new System.Drawing.Point(0, 5)
            };

            Label autoSaveExtensionLabel = new Label
            {
                Text = ".txt",
                AutoSize = true,
                Location = new System.Drawing.Point(205, 7)
            };

            autoSaveFileNamePanel.Controls.Add(autoSaveFileNameTextBox);
            autoSaveFileNamePanel.Controls.Add(autoSaveExtensionLabel);

            // Add all controls to the configuration layout
            configLayout.Controls.Add(saveLocationLabel, 0, 0);
            configLayout.Controls.Add(saveLocationPanel, 1, 0);
            configLayout.Controls.Add(formatLabel, 0, 1);
            configLayout.Controls.Add(formatPanel, 1, 1);
            configLayout.Controls.Add(protocolLabel, 0, 2);
            configLayout.Controls.Add(protocolPanel, 1, 2);
            configLayout.Controls.Add(autoSaveLabel, 0, 3);
            configLayout.Controls.Add(autoSavePanel, 1, 3);
            configLayout.Controls.Add(autoSaveFileNameLabel, 0, 4);
            configLayout.Controls.Add(autoSaveFileNamePanel, 1, 4);

            configGroup.Controls.Add(configLayout);

            // Status Panel (below configuration)
            Panel statusPanel = new Panel
            {
                Height = 30,
                Dock = DockStyle.Bottom,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Status indicator
            statusIndicator = new PictureBox
            {
                Size = new Size(16, 16),
                Location = new Point(10, 6),
                BackColor = Color.Red
            };

            statusIndicatorLabel = new Label
            {
                Text = "Not Listening",
                Location = new Point(32, 6),
                AutoSize = true
            };

            Button startButton = new Button
            {
                Text = "Start Listening",
                Width = 120,
                Location = new System.Drawing.Point(200, 3)
            };

            Button stopButton = new Button
            {
                Text = "Stop Listening",
                Width = 120,
                Enabled = false,
                Location = new System.Drawing.Point(330, 3)
            };

            statusPanel.Controls.Add(statusIndicator);
            statusPanel.Controls.Add(statusIndicatorLabel);
            statusPanel.Controls.Add(startButton);
            statusPanel.Controls.Add(stopButton);

            configGroup.Controls.Add(statusPanel);

            // Log Preview Group
            GroupBox logPreviewGroup = new GroupBox
            {
                Text = "Log Preview",
                Dock = DockStyle.Fill
            };

            TextBox logTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical
            };

            logPreviewGroup.Controls.Add(logTextBox);

            // Bottom Panel with buttons
            Panel bottomPanel = new Panel { Dock = DockStyle.Fill };
            Button clearLogsButton = new Button
            {
                Text = "Clear Logs",
                Width = 120,
                Height = 30,
                Location = new System.Drawing.Point(this.Width - 2 * 130, 5)
            };

            Button saveLogsButton = new Button
            {
                Text = "Save Logs",
                Width = 120,
                Height = 30,
                Location = new System.Drawing.Point(this.Width - 130, 5)
            };

            bottomPanel.Controls.Add(clearLogsButton);
            bottomPanel.Controls.Add(saveLogsButton);

            // Add everything to the main layout
            mainLayout.Controls.Add(configGroup, 0, 0);
            mainLayout.Controls.Add(statusPanel, 0, 1);
            mainLayout.Controls.Add(logPreviewGroup, 0, 2);
            mainLayout.Controls.Add(bottomPanel, 0, 3);

            // Update the format extension when the format changes
            formatComboBox.SelectedIndexChanged += (s, e) =>
            {
                switch (formatComboBox.SelectedIndex)
                {
                    case 0: selectedFormat = LogFormat.TXT; break;
                    case 1: selectedFormat = LogFormat.CSV; break;
                    case 2: selectedFormat = LogFormat.JSON; break;
                    case 3: selectedFormat = LogFormat.LOG; break;
                }

                // Update auto-save extension label
                autoSaveExtensionLabel.Text = GetFileExtension(selectedFormat);
            };

            // Event handlers
            browseButton.Click += (s, e) =>
            {
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.SelectedPath = savePath;
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        savePath = folderDialog.SelectedPath;
                        saveLocationTextBox.Text = savePath;
                    }
                }
            };

            tcpRadioButton.CheckedChanged += (s, e) =>
            {
                if (tcpRadioButton.Checked)
                    selectedProtocol = ProtocolType.TCP;
            };

            udpRadioButton.CheckedChanged += (s, e) =>
            {
                if (udpRadioButton.Checked)
                    selectedProtocol = ProtocolType.UDP;
            };

            autoSaveCheckBox.CheckedChanged += (s, e) =>
            {
                autoSaveEnabled = autoSaveCheckBox.Checked;
                autoSaveFileNameTextBox.Enabled = autoSaveEnabled;
                maxFileSizeUpDown.Enabled = autoSaveEnabled;
            };

            maxFileSizeUpDown.ValueChanged += (s, e) =>
            {
                maxLogFileSize = (int)maxFileSizeUpDown.Value;
            };

            startButton.Click += async (s, e) =>
            {
                try
                {
                    savePath = saveLocationTextBox.Text;
                    if (!Directory.Exists(savePath))
                    {
                        Directory.CreateDirectory(savePath);
                    }

                    startButton.Enabled = false;
                    stopButton.Enabled = true;
                    tcpRadioButton.Enabled = false;
                    udpRadioButton.Enabled = false;
                    formatComboBox.Enabled = false;
                    saveLocationTextBox.Enabled = false;
                    browseButton.Enabled = false;
                    autoSaveCheckBox.Enabled = false;
                    autoSaveFileNameTextBox.Enabled = false;
                    maxFileSizeUpDown.Enabled = false;

                    // Update status indicator
                    statusIndicator.BackColor = Color.Green;
                    statusIndicatorLabel.Text = $"Listening on {selectedProtocol} port {PORT}";

                    // Initialize auto-save if enabled
                    if (autoSaveEnabled)
                    {
                        InitializeAutoSave(autoSaveFileNameTextBox.Text);
                    }

                    cts = new CancellationTokenSource();

                    if (selectedProtocol == ProtocolType.TCP)
                        await StartTcpListeningAsync(cts.Token, logTextBox);
                    else
                        await StartUdpListeningAsync(cts.Token, logTextBox);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error starting service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    stopButton.PerformClick();
                }
            };

            stopButton.Click += (s, e) =>
            {
                StopListening(logTextBox);

                // Close auto-save writer if open
                CloseAutoSaveWriter();

                startButton.Enabled = true;
                stopButton.Enabled = false;
                tcpRadioButton.Enabled = true;
                udpRadioButton.Enabled = true;
                formatComboBox.Enabled = true;
                saveLocationTextBox.Enabled = true;
                browseButton.Enabled = true;
                autoSaveCheckBox.Enabled = true;
                autoSaveFileNameTextBox.Enabled = autoSaveEnabled;
                maxFileSizeUpDown.Enabled = autoSaveEnabled;

                // Update status indicator
                statusIndicator.BackColor = Color.Red;
                statusIndicatorLabel.Text = "Not Listening";
            };

            clearLogsButton.Click += (s, e) =>
            {
                logEntries.Clear();
                logTextBox.Clear();
            };

            saveLogsButton.Click += (s, e) =>
            {
                if (logEntries.Count == 0)
                {
                    MessageBox.Show("No logs to save.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    string fileName = $"Logs_{DateTime.Now:yyyyMMdd_HHmmss}";
                    string extension = GetFileExtension(selectedFormat);
                    string fullPath = Path.Combine(savePath, fileName + extension);

                    SaveLogs(fullPath);
                    MessageBox.Show($"Logs saved to: {fullPath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            // Handle resize events to reposition buttons
            this.Resize += (s, e) =>
            {
                clearLogsButton.Location = new System.Drawing.Point(this.ClientSize.Width - 2 * 130 - 10, 5);
                saveLogsButton.Location = new System.Drawing.Point(this.ClientSize.Width - 130 - 10, 5);
            };
        }

        private void InitializeAutoSave(string baseFileName)
        {
            try
            {
                CloseAutoSaveWriter(); // Close if already open

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{baseFileName}_{timestamp}";
                string extension = GetFileExtension(selectedFormat);
                autoSaveFileName = Path.Combine(savePath, fileName + extension);

                // Create directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(autoSaveFileName));

                // Create and initialize the file based on the format
                InitializeLogFile(autoSaveFileName);

                // Open the writer for ongoing writes
                autoSaveWriter = new StreamWriter(autoSaveFileName, true);

                // Reset counter
                logCountSinceLastSave = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing auto-save: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                autoSaveEnabled = false;
            }
        }

        private void InitializeLogFile(string filePath)
        {
            // Create the file with headers based on format
            using (var writer = new StreamWriter(filePath, false))
            {
                switch (selectedFormat)
                {
                    case LogFormat.CSV:
                        writer.WriteLine("Timestamp,Protocol,SourceIP,Message");
                        break;
                    case LogFormat.JSON:
                        writer.WriteLine("[");
                        writer.WriteLine("]");
                        break;
                        // For TXT and LOG formats, no initialization needed
                }
            }
        }

        private void WriteLogToAutoSaveFile(LogEntry entry)
        {
            if (!autoSaveEnabled || autoSaveWriter == null)
                return;

            try
            {
                lock (fileLock)
                {
                    // Check if we need to rotate the log file
                    if (File.Exists(autoSaveFileName))
                    {
                        var fileInfo = new FileInfo(autoSaveFileName);
                        if (fileInfo.Length > maxLogFileSize * 1024 * 1024)
                        {
                            // Close current writer
                            CloseAutoSaveWriter();

                            // If JSON format, we need to fix the JSON file by removing the last bracket and adding the closing bracket
                            if (selectedFormat == LogFormat.JSON)
                            {
                                FixJsonFile(autoSaveFileName);
                            }

                            // Start a new file
                            string baseFileName = Path.GetFileNameWithoutExtension(autoSaveFileName);
                            baseFileName = baseFileName.Substring(0, baseFileName.LastIndexOf('_')); // Remove timestamp
                            InitializeAutoSave(baseFileName);
                        }
                    }

                    // Write entry to file based on format
                    switch (selectedFormat)
                    {
                        case LogFormat.TXT:
                            autoSaveWriter.WriteLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Protocol}] {entry.SourceIP}: {entry.Message}");
                            break;
                        case LogFormat.CSV:
                            string escapedMessage = entry.Message.Replace("\"", "\"\"");
                            autoSaveWriter.WriteLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.Protocol},{entry.SourceIP},\"{escapedMessage}\"");
                            break;
                        case LogFormat.JSON:
                            // Remove the closing bracket, add the entry, and add the closing bracket back
                            autoSaveWriter.Flush();
                            using (var fileStream = new FileStream(autoSaveFileName, FileMode.Open, FileAccess.ReadWrite))
                            {
                                fileStream.SetLength(fileStream.Length - 2); // Remove the closing bracket
                            }
                            autoSaveWriter.Close();

                            using (var writer = new StreamWriter(autoSaveFileName, true))
                            {
                                string escapedJsonMessage = entry.Message.Replace("\"", "\\\"");
                                if (logEntries.Count > 1) // Not the first entry
                                    writer.Write(",\n");
                                writer.Write($"  {{\"timestamp\": \"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}\", \"protocol\": \"{entry.Protocol}\", \"source\": \"{entry.SourceIP}\", \"message\": \"{escapedJsonMessage}\"}}");
                                writer.WriteLine("\n]");
                            }

                            // Reopen writer
                            autoSaveWriter = new StreamWriter(autoSaveFileName, true);
                            break;
                        case LogFormat.LOG:
                            string month = entry.Timestamp.ToString("MMM");
                            string day = entry.Timestamp.Day.ToString().PadLeft(2, ' ');
                            string time = entry.Timestamp.ToString("HH:mm:ss");
                            autoSaveWriter.WriteLine($"{month} {day} {time} [{entry.Protocol}] {entry.SourceIP} {entry.Message}");
                            break;
                    }

                    autoSaveWriter.Flush();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing to auto-save file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Disable auto-save to prevent further errors
                autoSaveEnabled = false;
                CloseAutoSaveWriter();
            }
        }

        private void FixJsonFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);

                // Fix the JSON by ensuring it's properly terminated
                if (content.EndsWith(",\n]"))
                {
                    content = content.Substring(0, content.Length - 3) + "\n]";
                    File.WriteAllText(filePath, content);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fixing JSON file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CloseAutoSaveWriter()
        {
            if (autoSaveWriter != null)
            {
                autoSaveWriter.Flush();
                autoSaveWriter.Close();
                autoSaveWriter.Dispose();
                autoSaveWriter = null;

                // If it's a JSON file, make sure it's properly terminated
                if (selectedFormat == LogFormat.JSON && File.Exists(autoSaveFileName))
                {
                    FixJsonFile(autoSaveFileName);
                }
            }
        }

        private async Task StartTcpListeningAsync(CancellationToken token, TextBox logTextBox)
        {
            tcpListener = new TcpListener(IPAddress.Any, PORT);
            tcpListener.Start();
            AppendLog($"Started listening on TCP port {PORT}", "System", logTextBox);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var tcpClient = await tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = ProcessTcpClientAsync(tcpClient, (message, sourceIP) => AppendLog(message, sourceIP, logTextBox), token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    MessageBox.Show($"Error in TCP listener: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task StartUdpListeningAsync(CancellationToken token, TextBox logTextBox)
        {
            udpClient = new UdpClient(PORT);
            AppendLog($"Started listening on UDP port {PORT}", "System", logTextBox);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    UdpReceiveResult result = await udpClient.ReceiveAsync().ConfigureAwait(false);
                    string message = Encoding.UTF8.GetString(result.Buffer);
                    string sourceIP = result.RemoteEndPoint.Address.ToString();

                    AppendLog(message.Trim(), sourceIP, logTextBox);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    MessageBox.Show($"Error in UDP listener: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void StopListening(TextBox logTextBox)
        {
            cts?.Cancel();

            if (tcpListener != null)
            {
                tcpListener.Stop();
                tcpListener = null;
                AppendLog($"Stopped listening on TCP port {PORT}", "System", logTextBox);
            }

            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
                AppendLog($"Stopped listening on UDP port {PORT}", "System", logTextBox);
            }
        }

        private void AppendLog(string message, string sourceIP, TextBox logTextBox)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action<string, string, TextBox>(AppendLog), message, sourceIP, logTextBox);
                return;
            }

            var timestamp = DateTime.Now;
            var logEntry = new LogEntry
            {
                Timestamp = timestamp,
                Message = message,
                SourceIP = sourceIP,
                Protocol = selectedProtocol
            };
            logEntries.Add(logEntry);

            // Write to auto-save file if enabled
            if (autoSaveEnabled)
            {
                WriteLogToAutoSaveFile(logEntry);
            }

            string displayText = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [{selectedProtocol}] {sourceIP}: {message}";
            logTextBox.AppendText(displayText + Environment.NewLine);
            logTextBox.SelectionStart = logTextBox.Text.Length;
            logTextBox.ScrollToCaret();
        }

        private static async Task ProcessTcpClientAsync(TcpClient client, Action<string, string> logCallback, CancellationToken token)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    var remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                    string sourceIP = remoteEndPoint.Address.ToString();
                    logCallback($"Connection established", sourceIP);

                    var buffer = new char[4096];
                    int bytesRead;

                    while (!token.IsCancellationRequested && (bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        var message = new string(buffer, 0, bytesRead);
                        logCallback(message.Trim(), sourceIP);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    logCallback($"Error processing client: {ex.Message}", "System");
                }
            }
        }

        private string GetFileExtension(LogFormat format)
        {
            switch (format)
            {
                case LogFormat.TXT: return ".txt";
                case LogFormat.CSV: return ".csv";
                case LogFormat.JSON: return ".json";
                case LogFormat.LOG: return ".log";
                default: return ".txt";
            }
        }

        private void SaveLogs(string filePath)
        {
            switch (selectedFormat)
            {
                case LogFormat.TXT:
                    using (var writer = new StreamWriter(filePath))
                    {
                        foreach (var entry in logEntries)
                        {
                            writer.WriteLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Protocol}] {entry.SourceIP}: {entry.Message}");
                        }
                    }
                    break;
                case LogFormat.CSV:
                    using (var writer = new StreamWriter(filePath))
                    {
                        writer.WriteLine("Timestamp,Protocol,SourceIP,Message");
                        foreach (var entry in logEntries)
                        {
                            string escapedMessage = entry.Message.Replace("\"", "\"\"");
                            writer.WriteLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.Protocol},{entry.SourceIP},\"{escapedMessage}\"");
                        }
                    }
                    break;
                case LogFormat.JSON:
                    using (var writer = new StreamWriter(filePath))
                    {
                        writer.WriteLine("[");
                        for (int i = 0; i < logEntries.Count; i++)
                        {
                            var entry = logEntries[i];
                            string escapedMessage = entry.Message.Replace("\"", "\\\"");
                            writer.Write($"  {{\"timestamp\": \"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}\", \"protocol\": \"{entry.Protocol}\", \"source\": \"{entry.SourceIP}\", \"message\": \"{escapedMessage}\"}}");
                            if (i < logEntries.Count - 1)
                                writer.WriteLine(",");
                            else
                                writer.WriteLine();
                        }
                        writer.WriteLine("]");
                    }
                    break;
                case LogFormat.LOG:
                    using (var writer = new StreamWriter(filePath))
                    {
                        foreach (var entry in logEntries)
                        {
                            // Standard syslog format: <timestamp> <hostname> <message>
                            string month = entry.Timestamp.ToString("MMM");
                            string day = entry.Timestamp.Day.ToString().PadLeft(2, ' ');
                            string time = entry.Timestamp.ToString("HH:mm:ss");
                            writer.WriteLine($"{month} {day} {time} [{entry.Protocol}] {entry.SourceIP} {entry.Message}");
                        }
                    }
                    break;
            }
        }

        private enum LogFormat
        {
            TXT,
            CSV,
            JSON,
            LOG
        }

        private enum ProtocolType
        {
            TCP,
            UDP
        }

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Message { get; set; }
            public string SourceIP { get; set; }
            public ProtocolType Protocol { get; set; }
        }

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            // Clean up resources
            CloseAutoSaveWriter();

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(700, 590);
            this.Name = "MainForm";
            this.Text = "Log Collector";
            this.FormClosing += (s, e) =>
            {
                // Make sure to close auto-save writer when form is closing
                CloseAutoSaveWriter();
            };
            this.ResumeLayout(false);
        }
    }
}