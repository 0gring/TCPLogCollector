
Conversation opened. 1 read message.

Skip to content
Using Gmail with screen readers
10 of 2,984
קוד "מתוקן"‎
Inbox
Yonatan Doron
	
AttachmentsMon, Jul 14, 3:45 PM (4 days ago)
	
to me

 One attachment  •  Scanned by Gmail
	

﻿
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
using Newtonsoft.Json;


namespace TCPLogCollector
{
    public partial class MainForm : Form
    {
        private TcpListener tcpListener;
        private UdpClient udpClient;
        private CancellationTokenSource cts;
        private CircularBuffer<LogEntry> logEntries = new CircularBuffer<LogEntry>(1000); // Keep last 1000 logs
        private string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TCPLogs");
        private LogFormat selectedFormat = LogFormat.TXT;
        private ProtocolType selectedProtocol = ProtocolType.Tcp;
        private const int PORT = 514;
        private PictureBox statusIndicator;
        private Label statusIndicatorLabel;

        // Auto-save related fields
        private bool autoSaveEnabled = true;
        private string autoSaveFileName;
        private string autoSaveFilePath;
        private StreamWriter autoSaveWriter;
        private int autoSaveInterval = 1; // Save every X logs
        private int logCountSinceLastSave = 0;
        private int maxLogFileSize = 10; // MB
        private int maxLogFilesToRetain = 2; // Default number of log files to keep
        private CheckBox autoSaveCheckBox;
        private TextBox autoSaveFileNameTextBox;
        private NumericUpDown maxFileSizeUpDown;
        private readonly object fileLock = new object();
        private TextBox saveLocationTextBox;
        private Button startButton;
        private Button stopButton;
        private RadioButton tcpRadioButton;
        private RadioButton udpRadioButton;
        private ComboBox formatComboBox;
        private Button browseButton;


        public MainForm()
        {
            // Remove InitializeComponent if you don't use designer
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200)); // Configuration
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // Status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Log Preview
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));  // Bottom 


            this.Controls.Add(mainLayout);

            // Configuration Group
            GroupBox configGroup = new GroupBox
            {
                Text = "Configuration",
                Dock = DockStyle.Fill,
                Padding = new Padding(0)
            };

            // Create configuration layout
            TableLayoutPanel configLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 6, // Increased rows for auto-save settings
                ColumnCount = 2
            };
            // Modify the row styles to reduce the gap
            configLayout.RowStyles.Clear(); // Clear existing styles
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));
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
            saveLocationTextBox = new TextBox
            {
                Text = savePath,
                Width = 390,
                Location = new System.Drawing.Point(0, 5)
            };
            
            browseButton = new Button
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

            formatComboBox = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new System.Drawing.Point(0, 5)
            };
            formatComboBox.Items.AddRange(new object[] { "Text (.txt)", "CSV (.csv)", "JSON (.json)", "Syslog (.log)" });
            formatComboBox.SelectedIndex = 0;
            formatPanel.Controls.Add(formatComboBox);

            // Protocol selection controls
            Label protocolLabel = new Label
            {
                Text = "Protocol:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };

            Panel protocolPanel = new Panel { Dock = DockStyle.Fill };

            tcpRadioButton = new RadioButton
            {
                Text = "TCP",
                Checked = true,
                AutoSize = true,
                Location = new System.Drawing.Point(0, 5)
            };

            udpRadioButton = new RadioButton
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

            // Replace Panel with TableLayoutPanel for the row
            TableLayoutPanel autoSavePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true
            };
            autoSavePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // CheckBox
            autoSavePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            autoSavePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // NumericUpDown

            autoSaveCheckBox = new CheckBox
            {
                Text = "Enable Auto-Save",
                Checked = autoSaveEnabled,
                AutoSize = true,
                Margin = new Padding(0, 0, 10, 0) // right margin for spacing
            };

            Label maxFileSizeLabel = new Label
            {
                Text = "Max File Size (MB):",
                AutoSize = true,
                Margin = new Padding(0, 0, 10, 0)
            };

            maxFileSizeUpDown = new NumericUpDown
            {
                Value = maxLogFileSize,
                Minimum = 1,
                Maximum = 10000,
                Width = 60,
                Margin = new Padding(0, 0, 10, 0)
            };

            autoSavePanel.Controls.Add(autoSaveCheckBox, 0, 0);
            autoSavePanel.Controls.Add(maxFileSizeLabel, 1, 0);
            autoSavePanel.Controls.Add(maxFileSizeUpDown, 2, 0);


            // Add auto-save file name controls
            Label autoSaveFileNameLabel = new Label
            {
                Text = "Auto-Save File:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };

            Panel autoSaveFileNamePanel = new Panel { Dock = DockStyle.Fill };

            autoSaveFileNameTextBox = new TextBox
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

            // File Retention Controls
            Label fileRetentionLabel = new Label
            {
                Text = "Retain Files:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };

            Panel fileRetentionPanel = new Panel { Dock = DockStyle.Fill };

            NumericUpDown fileRetentionUpDown = new NumericUpDown
            {
                Value = maxLogFilesToRetain,
                Minimum = 1,
                Maximum = 100, // Arbitrary max number of files to retain
                Width = 60,
                Location = new System.Drawing.Point(0, 3)
            };

            Label filesLabel = new Label
            {
                Text = "files",
                AutoSize = true,
                Location = new System.Drawing.Point(65, 5)
            };

            fileRetentionPanel.Controls.Add(fileRetentionUpDown);
            fileRetentionPanel.Controls.Add(filesLabel);

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
            configLayout.Controls.Add(fileRetentionLabel, 0, 5);
            configLayout.Controls.Add(fileRetentionPanel, 1, 5);


            configGroup.Controls.Add(configLayout);

            // Status Panel (below configuration)
            Panel statusPanel = new Panel
            {
                Height = 40,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10, 5, 10, 5) // Add padding around all elements
            };
            // Add PictureBox, Label, Start/Stop buttons with spacing and alignment


            // Status indicator
            statusIndicator = new PictureBox
            {
                Size = new Size(16, 16),
                Location = new Point(15, 10),
                BackColor = Color.Red
            };

            statusIndicatorLabel = new Label
            {
                Text = "Not Listening",
                Location = new Point(40, 10),
                AutoSize = true
            };

            startButton = new Button
            {
                Text = "Start Listening",
                Width = 120,
                Location = new System.Drawing.Point(200, 3)
            };

            stopButton = new Button
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
          //  GroupBox logPreviewGroup = new GroupBox
          //  {
          //      Text = "Log Preview",
          //     Dock = DockStyle.Fill
          //  };

          //  TextBox logTextBox = new TextBox
          //  {
          //      Multiline = true,
          //      ReadOnly = true,
          //      Dock = DockStyle.Fill,
          //      ScrollBars = ScrollBars.Vertical
          //  };

          //  logPreviewGroup.Controls.Add(logTextBox);

            // Bottom Panel with buttons
            FlowLayoutPanel bottomPanel = new FlowLayoutPanel
            {
                     FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 5, 10, 0), // Top=5, Right=10 padding for spacing
                AutoSize = false,
                WrapContents = false
            };

        //    Button clearLogsButton = new Button
        //    {
        //        Text = "Clear Logs",
        //        Width = 120,
        //        Height = 30
        //    };

        //    Button saveLogsButton = new Button
        //    {
        //        Text = "Save Logs",
        //        Width = 120,
        //        Height = 30
        //    };

            // Add buttons in reverse order because FlowDirection is RightToLeft
        //    bottomPanel.Controls.Add(saveLogsButton);
        //    bottomPanel.Controls.Add(clearLogsButton);

            // Add everything to the main layout
            mainLayout.Controls.Add(configGroup, 0, 0);
            mainLayout.Controls.Add(statusPanel, 0, 1);      // <-- Add status panel here!
      //      mainLayout.Controls.Add(logPreviewGroup, 0, 2);
            mainLayout.Controls.Add(bottomPanel, 0, 3);
            // Save file retention value

            // Save file retention value
            fileRetentionUpDown.ValueChanged += (s, e) =>
            {
                maxLogFilesToRetain = (int)fileRetentionUpDown.Value;
            };

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
                    selectedProtocol = ProtocolType.Tcp;
            };

            udpRadioButton.CheckedChanged += (s, e) =>
            {
                if (udpRadioButton.Checked)
                    selectedProtocol = ProtocolType.Udp;
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
                    autoSaveFileName = autoSaveFileNameTextBox.Text;
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

                    if (selectedProtocol == ProtocolType.Tcp)
                        await StartTcpListeningAsync(cts.Token);
                    else
                        await StartUdpListeningAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error starting service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    stopButton.PerformClick();
                }
            };

            stopButton.Click += (s, e) =>
            {
                StopListening();
                statusIndicator.BackColor = Color.Red;
                statusIndicatorLabel.Text = "Not Listening";
                startButton.Enabled = true;
                stopButton.Enabled = false;
            };

        //    clearLogsButton.Click += (s, e) =>
          //  {
          //      logTextBox.Clear();
          //   };

          //  saveLogsButton.Click += (s, e) =>
          //  {
          //      SaveLogsToFile(logTextBox.Text);
          //  };

        }

        private void StopListening()
        {
            if (cts != null)
            {
                cts.Cancel();
            }

            if (tcpListener != null)
            {
                tcpListener.Stop();
            }

            if (udpClient != null)
            {
                udpClient.Close();
            }

            // Reset UI
            statusIndicator.BackColor = Color.Red;
            statusIndicatorLabel.Text = "Not Listening";
            startButton.Enabled = true;
            stopButton.Enabled = false;
            tcpRadioButton.Enabled = true;
            udpRadioButton.Enabled = true;
            formatComboBox.Enabled = true;
            saveLocationTextBox.Enabled = true;
            browseButton.Enabled = true;
            autoSaveCheckBox.Enabled = true;
            autoSaveFileNameTextBox.Enabled = true;
            maxFileSizeUpDown.Enabled = true;
        }

        private async Task StartTcpListeningAsync(CancellationToken token)
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, PORT);
                tcpListener.Start();

                while (!token.IsCancellationRequested)
                {
                    TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                    Task.Run(() => HandleTcpClientAsync(tcpClient, token));
                }
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.Interrupted)
                {
                    MessageBox.Show($"Socket error: {e.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (ObjectDisposedException)
            {
                // This exception occurs when the listener is stopped
                Console.WriteLine("TCP Listener stopped.");
            }
            finally
            {
                if (tcpListener != null)
                {
                    tcpListener.Stop();
                }
            }
        }

        private async Task HandleTcpClientAsync(TcpClient tcpClient, CancellationToken token)
        {
            try
            {
                using (NetworkStream stream = tcpClient.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
                {
                    while (!token.IsCancellationRequested)
                    {
                        string logMessage = await reader.ReadLineAsync();
                        if (logMessage == null) break;
                        AppendLog(logMessage);
                    }
                }
            }
            catch (IOException e)
            {
                Console.WriteLine($"IO Exception: {e.Message}");
            }
            finally
            {
                tcpClient.Close();
            }
        }

        private async Task StartUdpListeningAsync(CancellationToken token)
        {
            try
            {
                udpClient = new UdpClient(PORT);

                while (!token.IsCancellationRequested)
                {
                    UdpReceiveResult result = await udpClient.ReceiveAsync();
                    string logMessage = Encoding.ASCII.GetString(result.Buffer);
                    AppendLog(logMessage);
                }
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.Interrupted)
                {
                    MessageBox.Show($"Socket error: {e.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (ObjectDisposedException)
            {
                // This exception occurs when the UdpClient is stopped
                Console.WriteLine("UDP Listener stopped.");
            }
            finally
            {
                if (udpClient != null)
                {
                    udpClient.Close();
                }
            }
        }

        private void AppendLog(string logMessage)
        {
            if (string.IsNullOrEmpty(logMessage)) return;

            LogEntry newEntry = new LogEntry { Message = logMessage, Timestamp = DateTime.Now };

            lock (logEntries)
            {
                logEntries.Add(newEntry);
            }

            // Format the log message based on the selected format
            string formattedMessage = FormatLogMessage(newEntry);

            // Append to UI
          //  if (logTextBox.InvokeRequired)
          //  {
           //     logTextBox.Invoke(new Action(() =>
            //    {
            //        logTextBox.AppendText(formattedMessage + Environment.NewLine);
            //    }));
           // }
          //  else
          //  {
          //      logTextBox.AppendText(formattedMessage + Environment.NewLine);
          //  }

            // Auto-save functionality
            if (autoSaveEnabled)
            {
                logCountSinceLastSave++;
                if (logCountSinceLastSave >= autoSaveInterval)
                {
                    logCountSinceLastSave = 0;
                    WriteLog(formattedMessage);
                }
            }
        }

        private string FormatLogMessage(LogEntry logEntry)
        {
            switch (selectedFormat)
            {
                case LogFormat.TXT:
                    return $"{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss} - {logEntry.Message}";
                case LogFormat.CSV:
                    return $"{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss},{logEntry.Message.Replace(",", "")}"; // Remove commas from message
                case LogFormat.JSON:
                    return Newtonsoft.Json.JsonConvert.SerializeObject(logEntry);
                case LogFormat.LOG:
                    return $"<{DateTime.Now.Millisecond}>{DateTime.Now:yyyy-MM-ddTHH:mm:sszzz} {Dns.GetHostName()} {logEntry.Message}";
                default:
                    return logEntry.Message; // Default to plain text
            }
        }

        private void SaveLogsToFile(string logs)
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv|JSON files (*.json)|*.json|Log files (*.log)|*.log|All files (*.*)|*.*";
                    saveFileDialog.FileName = "Log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(saveFileDialog.FileName, logs);
                        MessageBox.Show("Logs saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeAutoSave(string fileName)
        {
            // Construct the complete file name based on the current date and time
            autoSaveFilePath = Path.Combine(savePath, $"{fileName}_{DateTime.Now:yyyyMMdd_HHmmss}{GetFileExtension(selectedFormat)}");

            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(savePath);

                // Create the file and the stream writer
                autoSaveWriter = new StreamWriter(autoSaveFilePath, true); // 'true' for appending
                autoSaveWriter.AutoFlush = true; // Ensure data is written immediately
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing auto-save: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                autoSaveEnabled = false; // Disable auto-save if initialization fails
            }
        }

        private async void WriteLog(string logMessage)
        {
            if (!autoSaveEnabled) return;

            // Check if the autoSaveWriter is null
            if (autoSaveWriter == null)
            {
                InitializeAutoSave(autoSaveFileName);
                if (autoSaveWriter == null) // Check again in case initialization failed
                {
                    return; // Exit if still failed
                }
            }

            // Use a lock to handle file access from multiple threads
            lock (fileLock)
            {
                try
                {
                    // Get the file info
                    FileInfo fileInfo = new FileInfo(autoSaveFilePath);

                    // Check if the file exists and has reached its maximum size
                    if (fileInfo.Exists && fileInfo.Length > maxLogFileSize * 1024 * 1024)
                    {
                        // Close the current writer
                        autoSaveWriter.Close();

                        // Initialize a new auto-save file
                        InitializeAutoSave(autoSaveFileName);

                        // Remove old log files
                        CleanupOldLogFiles();
                    }

                    // Write the log message
                    autoSaveWriter.WriteLine(logMessage);
                    autoSaveWriter.Flush(); // Ensure the message is written immediately
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error writing to log file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    autoSaveEnabled = false; // Optionally disable auto-save on error
                }
            }
        }

        private void CleanupOldLogFiles()
        {
            try
            {
                // Get all log files in the directory
                DirectoryInfo directory = new DirectoryInfo(savePath);
                FileInfo[] logFiles = directory.GetFiles($"AutoSave_*{GetFileExtension(selectedFormat)}");

                // Sort the files by creation time, oldest first
                Array.Sort(logFiles, (f1, f2) => f1.CreationTime.CompareTo(f2.CreationTime));

                // Determine how many files to delete
                int filesToDelete = logFiles.Length - maxLogFilesToRetain;

                // Delete the oldest files
                for (int i = 0; i < filesToDelete; i++)
                {
                    logFiles[i].Delete();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cleaning up old log files: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetFileExtension(LogFormat format)
        {
            switch (format)
            {
                case LogFormat.TXT:
                    return ".txt";
                case LogFormat.CSV:
                    return ".csv";
                case LogFormat.JSON:
                    return ".json";
                case LogFormat.LOG:
                    return ".log";
                default:
                    return ".txt"; // Default to .txt
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }

    // Define the LogFormat enum
    public enum LogFormat
    {
        TXT,
        CSV,
        JSON,
        LOG
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
    }
}

MainForm2.cs
Displaying MainForm2.cs.
