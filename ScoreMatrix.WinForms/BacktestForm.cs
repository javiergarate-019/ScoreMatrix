using System.Globalization;
using ScoreMatrix.Application;
using ScoreMatrix.Domain;

namespace ScoreMatrix.WinForms;

public sealed class BacktestForm : Form
{
    private readonly ScoreMatrixCalculator _calculator;
    private readonly CsvExporter _csvExporter;

    private readonly Button _loadCsvButton = new() { Text = "Cargar CSV...", Dock = DockStyle.Fill };
    private readonly Label _fileLabel = new() { Text = "Ningun archivo cargado.", AutoSize = true, Dock = DockStyle.Fill };
    private readonly ComboBox _modelCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _devigCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _rhoInput = CreateNumeric(-0.05m, -0.25m, 0.25m, 3, 0.005m);
    private readonly CheckBox _autoCalibrateCheck = new() { Text = "Auto-calibrar rho", AutoSize = true };
    private readonly Button _runButton = new() { Text = "Ejecutar backtest", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _exportButton = new() { Text = "Exportar CSV", Dock = DockStyle.Fill, Enabled = false };
    private readonly Label _metricsLabel = new() { AutoSize = true, Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Regular) };
    private readonly ListView _matchList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, VirtualMode = false };
    private readonly ListView _calibrationList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };

    private IReadOnlyList<MatchRecord>? _records;
    private BacktestReport? _lastReport;

    public BacktestForm(ScoreMatrixCalculator calculator, CsvExporter csvExporter)
    {
        _calculator = calculator;
        _csvExporter = csvExporter;

        Text = "ScoreMatrix - Backtest";
        Size = new Size(1100, 740);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;

        _modelCombo.Items.AddRange(["Poisson", "Dixon-Coles", "Bivariado Poisson", "Binomial Negativa"]);
        _modelCombo.SelectedIndex = 0;
        _devigCombo.Items.AddRange(["Proporcional", "Power", "Shin", "Aditivo"]);
        _devigCombo.SelectedIndex = 0;

        _matchList.Columns.Add("Local", 110);
        _matchList.Columns.Add("Visitante", 110);
        _matchList.Columns.Add("P(Local)", 72);
        _matchList.Columns.Add("P(Empate)", 72);
        _matchList.Columns.Add("P(Visit.)", 72);
        _matchList.Columns.Add("λ Local", 70);
        _matchList.Columns.Add("λ Visit.", 70);
        _matchList.Columns.Add("Dep.", 60);
        _matchList.Columns.Add("Resultado", 80);
        _matchList.Columns.Add("RPS", 72);
        _matchList.Columns.Add("Brier", 72);

        _calibrationList.Columns.Add("Bin", 60);
        _calibrationList.Columns.Add("Pronosticado", 110);
        _calibrationList.Columns.Add("Observado", 110);
        _calibrationList.Columns.Add("N", 60);

        BuildLayout();
        WireEvents();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Row 0: file selector
        var fileRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileRow.Controls.Add(_loadCsvButton, 0, 0);
        fileRow.Controls.Add(_fileLabel, 1, 0);
        _fileLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(fileRow, 0, 0);

        // Row 1: model/devig/rho options
        var optRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 1 };
        for (var i = 0; i < 10; i++) optRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f));
        AddField(optRow, "Modelo", _modelCombo, 0, 0);
        AddField(optRow, "De-vig", _devigCombo, 2, 0);
        AddField(optRow, "Rho / Dep.", _rhoInput, 4, 0);
        optRow.Controls.Add(_autoCalibrateCheck, 6, 0);
        _autoCalibrateCheck.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        optRow.SetColumnSpan(_autoCalibrateCheck, 2);
        root.Controls.Add(optRow, 0, 1);

        // Row 2: run + export + metrics summary
        var actionRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 1 };
        for (var i = 0; i < 10; i++) actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f));
        actionRow.Controls.Add(_runButton, 0, 0);
        actionRow.SetColumnSpan(_runButton, 2);
        actionRow.Controls.Add(_exportButton, 2, 0);
        actionRow.SetColumnSpan(_exportButton, 2);
        actionRow.Controls.Add(_metricsLabel, 4, 0);
        actionRow.SetColumnSpan(_metricsLabel, 6);
        root.Controls.Add(actionRow, 0, 2);

        // Row 3: match results + calibration curve
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 780 };

        var matchPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        matchPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        matchPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        matchPanel.Controls.Add(new Label { Text = "Resultados por partido", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        matchPanel.Controls.Add(_matchList, 0, 1);
        split.Panel1.Controls.Add(matchPanel);

        var calPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        calPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        calPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        calPanel.Controls.Add(new Label { Text = "Curva de calibracion (victoria local)", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        calPanel.Controls.Add(_calibrationList, 0, 1);
        split.Panel2.Controls.Add(calPanel);

        root.Controls.Add(split, 0, 3);
        Controls.Add(root);
    }

    private void WireEvents()
    {
        _loadCsvButton.Click += (_, _) => LoadCsv();
        _runButton.Click += async (_, _) => await RunBacktestAsync();
        _exportButton.Click += (_, _) => ExportReport();
        _modelCombo.SelectedIndexChanged += (_, _) => UpdateState();
        _autoCalibrateCheck.CheckedChanged += (_, _) => UpdateState();
        _devigCombo.SelectedIndexChanged += (_, _) => UpdateState();
        UpdateState();
    }

    private void UpdateState()
    {
        var modelIdx = _modelCombo.SelectedIndex;
        var hasDep = modelIdx is 1 or 2 or 3;
        _rhoInput.Enabled = hasDep && !_autoCalibrateCheck.Checked;
        _autoCalibrateCheck.Enabled = modelIdx is 1 or 2;
        if (!_autoCalibrateCheck.Enabled) _autoCalibrateCheck.Checked = false;
        _runButton.Enabled = _records != null;
    }

    private void LoadCsv()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
            Title = "Cargar CSV de partidos historicos"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var content = File.ReadAllText(dialog.FileName);
            _records = BacktestCsvReader.Read(content);
            _fileLabel.Text = $"{Path.GetFileName(dialog.FileName)} — {_records.Count} partidos cargados.";
            _runButton.Enabled = true;
            _lastReport = null;
            _exportButton.Enabled = false;
            _matchList.Items.Clear();
            _calibrationList.Items.Clear();
            _metricsLabel.Text = string.Empty;
        }
        catch (ScoreMatrixValidationException ex)
        {
            MessageBox.Show(this, ex.Message, "Error al cargar CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunBacktestAsync()
    {
        if (_records is null) return;

        _runButton.Enabled = false;
        _exportButton.Enabled = false;
        _metricsLabel.Text = "Calculando...";
        UseWaitCursor = true;

        try
        {
            var modelType = _modelCombo.SelectedIndex switch
            {
                1 => ScoreModelType.DixonColes,
                2 => ScoreModelType.BivariatePoisson,
                3 => ScoreModelType.NegativeBinomial,
                _ => ScoreModelType.Poisson
            };
            var devigMethod = _devigCombo.SelectedIndex switch
            {
                1 => DevigMethod.Power,
                2 => DevigMethod.Shin,
                3 => DevigMethod.Additive,
                _ => DevigMethod.Proportional
            };
            var rho = (double)_rhoInput.Value;
            var autoCalibrate = _autoCalibrateCheck.Checked;
            var records = _records;

            var report = await Task.Run(() =>
            {
                var backtester = new Backtester(_calculator);
                return backtester.Run(records, modelType, devigMethod, rho, autoCalibrate);
            });

            _lastReport = report;
            RenderReport(report);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error en backtest", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _metricsLabel.Text = "Error al ejecutar backtest.";
        }
        finally
        {
            UseWaitCursor = false;
            _runButton.Enabled = _records != null;
            _exportButton.Enabled = _lastReport != null;
        }
    }

    private void RenderReport(BacktestReport report)
    {
        _metricsLabel.Text =
            $"Partidos: {report.Matches.Count}   " +
            $"RPS medio: {report.MeanRps:0.0000}   " +
            $"Brier medio: {report.MeanBrier:0.0000}   " +
            $"LogLoss medio: {report.MeanLogLoss:0.0000}";

        _matchList.BeginUpdate();
        _matchList.Items.Clear();
        foreach (var m in report.Matches)
        {
            var item = new ListViewItem(m.HomeTeam);
            item.SubItems.Add(m.AwayTeam);
            item.SubItems.Add(FormatPct(m.HomeWinProb));
            item.SubItems.Add(FormatPct(m.DrawProb));
            item.SubItems.Add(FormatPct(m.AwayWinProb));
            item.SubItems.Add(m.LambdaHome.ToString("0.00", CultureInfo.InvariantCulture));
            item.SubItems.Add(m.LambdaAway.ToString("0.00", CultureInfo.InvariantCulture));
            item.SubItems.Add(m.EstimatedDependence.ToString("0.000", CultureInfo.InvariantCulture));
            item.SubItems.Add(m.Score);
            item.SubItems.Add(m.Rps.ToString("0.0000", CultureInfo.InvariantCulture));
            item.SubItems.Add(m.Brier.ToString("0.0000", CultureInfo.InvariantCulture));
            _matchList.Items.Add(item);
        }

        _matchList.EndUpdate();

        _calibrationList.BeginUpdate();
        _calibrationList.Items.Clear();
        foreach (var bin in report.CalibrationCurve)
        {
            var item = new ListViewItem($"{bin.Midpoint:0.0#}");
            item.SubItems.Add(FormatPct(bin.MeanForecast));
            item.SubItems.Add(FormatPct(bin.ObservedFrequency));
            item.SubItems.Add(bin.Count.ToString(CultureInfo.InvariantCulture));
            _calibrationList.Items.Add(item);
        }

        _calibrationList.EndUpdate();
    }

    private void ExportReport()
    {
        if (_lastReport is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = "backtest-report.csv",
            AddExtension = true,
            DefaultExt = "csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        File.WriteAllText(dialog.FileName, _csvExporter.ExportBacktest(_lastReport));
    }

    private static void AddField(TableLayoutPanel panel, string label, Control control, int column, int row)
    {
        var wrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(2, 0, 8, 4)
        };
        wrapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        wrapper.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        wrapper.Controls.Add(control, 0, 1);
        panel.Controls.Add(wrapper, column, row);
        panel.SetColumnSpan(wrapper, 2);
    }

    private static NumericUpDown CreateNumeric(decimal value, decimal min, decimal max, int decimals, decimal increment = 0.01m)
        => new()
        {
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimals,
            Increment = increment,
            ThousandsSeparator = false,
            Value = value
        };

    private static string FormatPct(double v) => $"{v * 100:0.00}%";
}
