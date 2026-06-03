using System.Globalization;
using ScoreMatrix.Application;
using ScoreMatrix.Domain;

namespace ScoreMatrix.WinForms;

public sealed class Form1 : Form
{
    private readonly ScoreMatrixCalculator _calculator = new(new OddsConverter(), new LambdaOptimizer());
    private readonly CsvExporter _csvExporter = new();
    private readonly ToolTip _toolTip = new()
    {
        AutoPopDelay = 12000,
        InitialDelay = 400,
        ReshowDelay = 100,
        ShowAlways = true
    };

    private readonly TextBox _homeTeamText = new() { Text = "Local" };
    private readonly TextBox _awayTeamText = new() { Text = "Visitante" };
    private readonly RadioButton _oddsModeRadio = new() { Text = "Odds 1X2", Checked = true, AutoSize = true };
    private readonly RadioButton _lambdaModeRadio = new() { Text = "Goles esperados", AutoSize = true };
    private readonly NumericUpDown _homeOddsInput = CreateNumeric(2.10m, 1.01m, 100m, 2);
    private readonly NumericUpDown _drawOddsInput = CreateNumeric(3.25m, 1.01m, 100m, 2);
    private readonly NumericUpDown _awayOddsInput = CreateNumeric(3.60m, 1.01m, 100m, 2);
    private readonly CheckBox _useOverUnder25Input = new() { Text = "Usar O/U 2.5", AutoSize = true };
    private readonly NumericUpDown _over25OddsInput = CreateNumeric(1.90m, 1.01m, 100m, 2);
    private readonly NumericUpDown _under25OddsInput = CreateNumeric(1.90m, 1.01m, 100m, 2);
    private readonly CheckBox _useBothTeamsToScoreInput = new() { Text = "Usar BTTS", AutoSize = true };
    private readonly NumericUpDown _bothTeamsToScoreYesOddsInput = CreateNumeric(1.90m, 1.01m, 100m, 2);
    private readonly NumericUpDown _bothTeamsToScoreNoOddsInput = CreateNumeric(1.90m, 1.01m, 100m, 2);
    private readonly NumericUpDown _homeLambdaInput = CreateNumeric(1.40m, 0.01m, 6m, 2);
    private readonly NumericUpDown _awayLambdaInput = CreateNumeric(1.10m, 0.01m, 6m, 2);
    private readonly NumericUpDown _maxGoalsInput = CreateNumeric(6m, 3m, 15m, 0);
    private readonly ComboBox _modelCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _rhoInput = CreateNumeric(-0.05m, -0.25m, 0.25m, 3, 0.005m);
    private readonly NumericUpDown _marginInput = CreateNumeric(5m, 0m, 50m, 2, 0.25m);
    private readonly ComboBox _displayCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _lambdaLabel = new() { AutoSize = true };
    private readonly DataGridView _matrixGrid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, RowHeadersWidth = 72 };
    private readonly ListView _summaryList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };

    private ScoreMatrixResult? _lastResult;

    public Form1()
    {
        Text = "ScoreMatrix";
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? Icon;
        MinimumSize = new Size(1180, 820);
        Size = new Size(1280, 860);
        StartPosition = FormStartPosition.CenterScreen;

        _modelCombo.Items.AddRange(["Poisson", "Dixon-Coles"]);
        _modelCombo.SelectedIndex = 0;
        _displayCombo.Items.AddRange(["Probabilidades", "Odds justas", "Odds con margen"]);
        _displayCombo.SelectedIndex = 0;

        BuildLayout();
        ConfigureToolTips();
        WireEvents();
        CalculateAndRender();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 430));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));

        root.Controls.Add(BuildInputPanel(), 0, 0);
        root.Controls.Add(_matrixGrid, 0, 1);
        root.Controls.Add(BuildSummaryPanel(), 0, 2);
        Controls.Add(root);

        _matrixGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _matrixGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _matrixGrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _matrixGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _matrixGrid.TopLeftHeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        _summaryList.Columns.Add("Mercado", 240);
        _summaryList.Columns.Add("Probabilidad", 140);
        _summaryList.Columns.Add("Odd justa", 120);
        _summaryList.Columns.Add("Odd con margen", 140);
    }

    private Control BuildInputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 10,
            RowCount = 5
        };

        for (var i = 0; i < 10; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        AddField(panel, "Equipo local", _homeTeamText, 0, 0);
        AddField(panel, "Equipo visitante", _awayTeamText, 2, 0);
        panel.Controls.Add(_oddsModeRadio, 4, 0);
        _oddsModeRadio.Anchor = AnchorStyles.Left;
        panel.Controls.Add(_lambdaModeRadio, 5, 0);
        _lambdaModeRadio.Anchor = AnchorStyles.Left;
        _lambdaLabel.Dock = DockStyle.Fill;
        _lambdaLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(_lambdaLabel, 6, 0);
        panel.SetColumnSpan(_lambdaLabel, 4);

        var oddsGroup = CreateGroupBox("Mercado 1X2", topPadding: 4);
        var oddsPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 58, ColumnCount = 6, RowCount = 1, Margin = new Padding(0) };
        for (var i = 0; i < 6; i++)
        {
            oddsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
        }

        AddField(oddsPanel, "Odd local", _homeOddsInput, 0, 0);
        AddField(oddsPanel, "Odd empate", _drawOddsInput, 2, 0);
        AddField(oddsPanel, "Odd visitante", _awayOddsInput, 4, 0);
        oddsGroup.Controls.Add(oddsPanel);
        panel.Controls.Add(oddsGroup, 0, 1);
        panel.SetColumnSpan(oddsGroup, 10);

        var calibrationGroup = CreateGroupBox("Calibracion opcional", topPadding: 10);
        var calibrationPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 104, ColumnCount = 8, RowCount = 2, Margin = new Padding(0) };
        for (var i = 0; i < 8; i++)
        {
            calibrationPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
        }

        calibrationPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        calibrationPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        calibrationPanel.Controls.Add(_useOverUnder25Input, 0, 0);
        calibrationPanel.SetColumnSpan(_useOverUnder25Input, 2);
        _useOverUnder25Input.Anchor = AnchorStyles.Left;
        AddField(calibrationPanel, "Odd Over 2.5", _over25OddsInput, 2, 0);
        AddField(calibrationPanel, "Odd Under 2.5", _under25OddsInput, 4, 0);
        calibrationPanel.Controls.Add(_useBothTeamsToScoreInput, 0, 1);
        calibrationPanel.SetColumnSpan(_useBothTeamsToScoreInput, 2);
        _useBothTeamsToScoreInput.Anchor = AnchorStyles.Left;
        AddField(calibrationPanel, "Odd BTTS Si", _bothTeamsToScoreYesOddsInput, 2, 1);
        AddField(calibrationPanel, "Odd BTTS No", _bothTeamsToScoreNoOddsInput, 4, 1);
        calibrationGroup.Controls.Add(calibrationPanel);
        panel.Controls.Add(calibrationGroup, 0, 2);
        panel.SetColumnSpan(calibrationGroup, 10);

        AddField(panel, "Lambda local", _homeLambdaInput, 0, 3);
        AddField(panel, "Lambda visitante", _awayLambdaInput, 2, 3);
        AddField(panel, "Max goles", _maxGoalsInput, 4, 3);
        AddField(panel, "Modelo", _modelCombo, 6, 3);

        AddField(panel, "Rho", _rhoInput, 8, 3);
        AddField(panel, "Vista", _displayCombo, 0, 4);
        AddField(panel, "Margen %", _marginInput, 2, 4);

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(2, 6, 8, 10)
        };
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        var calculateButton = new Button { Text = "Calcular", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        calculateButton.Click += (_, _) => CalculateAndRender();
        buttonPanel.Controls.Add(calculateButton, 0, 0);

        var exportButton = new Button { Text = "Exportar CSV", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        exportButton.Click += (_, _) => ExportCsv();
        buttonPanel.Controls.Add(exportButton, 1, 0);

        var helpButton = new Button { Text = "Ayuda", Dock = DockStyle.Fill, Margin = new Padding(0) };
        helpButton.Click += (_, _) => ShowHelp();
        buttonPanel.Controls.Add(helpButton, 2, 0);
        panel.Controls.Add(buttonPanel, 4, 4);
        panel.SetColumnSpan(buttonPanel, 6);

        return panel;
    }

    private static GroupBox CreateGroupBox(string title, int topPadding = 18)
        => new()
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(10, topPadding, 10, 8),
            Margin = new Padding(2, 0, 8, 6)
        };

    private Control BuildSummaryPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = "Resumen agregado", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        panel.Controls.Add(_summaryList, 0, 1);
        return panel;
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
        wrapper.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        wrapper.Controls.Add(control, 0, 1);
        panel.Controls.Add(wrapper, column, row);
        panel.SetColumnSpan(wrapper, 2);
    }

    private void ConfigureToolTips()
    {
        SetTip(_homeTeamText, "Nombre del equipo local. Solo se usa para rotular la matriz y el CSV.");
        SetTip(_awayTeamText, "Nombre del equipo visitante. Solo se usa para rotular la matriz y el CSV.");
        SetTip(_oddsModeRadio, "Calcula los goles esperados a partir de las cuotas 1X2 ingresadas.");
        SetTip(_lambdaModeRadio, "Usa directamente los goles esperados que ingreses para cada equipo.");
        SetTip(_homeOddsInput, "Cuota decimal para victoria local. Debe ser mayor que 1.");
        SetTip(_drawOddsInput, "Cuota decimal para empate. Debe ser mayor que 1.");
        SetTip(_awayOddsInput, "Cuota decimal para victoria visitante. Debe ser mayor que 1.");
        SetTip(_useOverUnder25Input, "Activa cuotas Over/Under 2.5 para calibrar mejor el total esperado de goles.");
        SetTip(_over25OddsInput, "Cuota decimal para Over 2.5. Se usa solo si Usar O/U 2.5 esta activado.");
        SetTip(_under25OddsInput, "Cuota decimal para Under 2.5. Se usa solo si Usar O/U 2.5 esta activado.");
        SetTip(_useBothTeamsToScoreInput, "Activa cuotas BTTS Si/No para calibrar si ambos equipos marcan.");
        SetTip(_bothTeamsToScoreYesOddsInput, "Cuota decimal para BTTS Si. Se usa solo si Usar BTTS esta activado.");
        SetTip(_bothTeamsToScoreNoOddsInput, "Cuota decimal para BTTS No. Se usa solo si Usar BTTS esta activado.");
        SetTip(_homeLambdaInput, "Goles esperados del local. Se usa solo en modo Goles esperados.");
        SetTip(_awayLambdaInput, "Goles esperados del visitante. Se usa solo en modo Goles esperados.");
        SetTip(_maxGoalsInput, "Mayor marcador que se muestra en la matriz. Puede estar entre 3 y 15.");
        SetTip(_modelCombo, "Modelo matematico para calcular la matriz: Poisson simple o Dixon-Coles.");
        SetTip(_rhoInput, "Parametro Dixon-Coles para ajustar marcadores bajos. Se usa solo con el modelo Dixon-Coles.");
        SetTip(_marginInput, "Margen comercial que se aplica sobre las odds justas al usar la vista Odds con margen. Usa 0% si queres ver la cuota justa sin descuento.");
        SetTip(_displayCombo, "Probabilidades muestra el porcentaje estimado. Odds justas muestra 1 dividido por esa probabilidad. Odds con margen descuenta el margen indicado.");
        SetTip(_matrixGrid, "Cada celda muestra la probabilidad estimada del marcador exacto o su cuota justa, segun la vista elegida.");
        SetTip(_summaryList, "Probabilidad es la chance estimada del mercado agregado. Odd justa no incluye margen. Odd con margen descuenta el margen indicado.");
    }

    private void SetTip(Control control, string text) => _toolTip.SetToolTip(control, text);

    private void WireEvents()
    {
        _oddsModeRadio.CheckedChanged += (_, _) =>
        {
            UpdateInputState();
            CalculateAndRender();
        };
        _lambdaModeRadio.CheckedChanged += (_, _) =>
        {
            UpdateInputState();
            CalculateAndRender();
        };
        _modelCombo.SelectedIndexChanged += (_, _) =>
        {
            UpdateInputState();
            CalculateAndRender();
        };
        _displayCombo.SelectedIndexChanged += (_, _) => RenderResult();
        _marginInput.ValueChanged += (_, _) => UpdateMarginDisplay();
        _homeOddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _drawOddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _awayOddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _useOverUnder25Input.CheckedChanged += (_, _) =>
        {
            UpdateInputState();
            CalculateAndRender();
        };
        _over25OddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _under25OddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _useBothTeamsToScoreInput.CheckedChanged += (_, _) =>
        {
            UpdateInputState();
            CalculateAndRender();
        };
        _bothTeamsToScoreYesOddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _bothTeamsToScoreNoOddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _homeLambdaInput.ValueChanged += (_, _) => CalculateAndRender();
        _awayLambdaInput.ValueChanged += (_, _) => CalculateAndRender();
        _maxGoalsInput.ValueChanged += (_, _) => CalculateAndRender();
        _rhoInput.ValueChanged += (_, _) => CalculateAndRender();
        _homeTeamText.TextChanged += (_, _) => CalculateAndRender();
        _awayTeamText.TextChanged += (_, _) => CalculateAndRender();
        UpdateInputState();
    }

    private void UpdateMarginDisplay()
    {
        if (_lastResult is null)
        {
            return;
        }

        _lastResult = _lastResult with { BookmakerMarginPercent = (double)_marginInput.Value };
        RenderResult();
    }

    private void UpdateInputState()
    {
        var oddsMode = _oddsModeRadio.Checked;
        _homeOddsInput.Enabled = oddsMode;
        _drawOddsInput.Enabled = oddsMode;
        _awayOddsInput.Enabled = oddsMode;
        _useOverUnder25Input.Enabled = oddsMode;
        _over25OddsInput.Enabled = oddsMode && _useOverUnder25Input.Checked;
        _under25OddsInput.Enabled = oddsMode && _useOverUnder25Input.Checked;
        _useBothTeamsToScoreInput.Enabled = oddsMode;
        _bothTeamsToScoreYesOddsInput.Enabled = oddsMode && _useBothTeamsToScoreInput.Checked;
        _bothTeamsToScoreNoOddsInput.Enabled = oddsMode && _useBothTeamsToScoreInput.Checked;
        _homeLambdaInput.Enabled = !oddsMode;
        _awayLambdaInput.Enabled = !oddsMode;
        _rhoInput.Enabled = _modelCombo.SelectedIndex == 1;
    }

    private void CalculateAndRender()
    {
        try
        {
            _lastResult = _calculator.Calculate(ReadInput());
            RenderResult();
        }
        catch (ScoreMatrixValidationException ex)
        {
            MessageBox.Show(this, ex.Message, "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private MatchInput ReadInput()
        => new()
        {
            HomeTeamName = _homeTeamText.Text,
            AwayTeamName = _awayTeamText.Text,
            InputMode = _oddsModeRadio.Checked ? InputMode.Odds1X2 : InputMode.ExpectedGoals,
            HomeOdds = (double)_homeOddsInput.Value,
            DrawOdds = (double)_drawOddsInput.Value,
            AwayOdds = (double)_awayOddsInput.Value,
            UseOverUnder25Odds = _useOverUnder25Input.Checked,
            Over25Odds = (double)_over25OddsInput.Value,
            Under25Odds = (double)_under25OddsInput.Value,
            UseBothTeamsToScoreOdds = _useBothTeamsToScoreInput.Checked,
            BothTeamsToScoreYesOdds = (double)_bothTeamsToScoreYesOddsInput.Value,
            BothTeamsToScoreNoOdds = (double)_bothTeamsToScoreNoOddsInput.Value,
            HomeExpectedGoals = (double)_homeLambdaInput.Value,
            AwayExpectedGoals = (double)_awayLambdaInput.Value,
            ModelType = _modelCombo.SelectedIndex == 1 ? ScoreModelType.DixonColes : ScoreModelType.Poisson,
            MaxGoals = (int)_maxGoalsInput.Value,
            DisplayMode = _displayCombo.SelectedIndex switch
            {
                1 => DisplayMode.FairOdds,
                2 => DisplayMode.OddsWithMargin,
                _ => DisplayMode.Probabilities
            },
            BookmakerMarginPercent = (double)_marginInput.Value,
            DixonColesRho = (double)_rhoInput.Value
        };

    private void RenderResult()
    {
        if (_lastResult is null)
        {
            return;
        }

        _lambdaLabel.Text = $"Lambdas usados: local {_lastResult.LambdaHome:0.000} | visitante {_lastResult.LambdaAway:0.000}";
        RenderMatrix(_lastResult);
        RenderSummary(_lastResult);
    }

    private void RenderMatrix(ScoreMatrixResult result)
    {
        var maxGoals = result.Scores.Max(score => Math.Max(score.HomeGoals, score.AwayGoals));
        _matrixGrid.Columns.Clear();
        _matrixGrid.Rows.Clear();

        for (var awayGoals = 0; awayGoals <= maxGoals; awayGoals++)
        {
            _matrixGrid.Columns.Add($"away{awayGoals}", awayGoals.ToString(CultureInfo.InvariantCulture));
        }

        for (var homeGoals = 0; homeGoals <= maxGoals; homeGoals++)
        {
            var values = new object[maxGoals + 1];
            for (var awayGoals = 0; awayGoals <= maxGoals; awayGoals++)
            {
                var score = result.Scores.First(item => item.HomeGoals == homeGoals && item.AwayGoals == awayGoals);
                values[awayGoals] = _displayCombo.SelectedIndex switch
                {
                    1 => FormatOdds(score.Probability),
                    2 => FormatOddsWithMargin(score.Probability, result.BookmakerMarginPercent),
                    _ => FormatPercent(score.Probability)
                };
            }

            var row = _matrixGrid.Rows.Add(values);
            _matrixGrid.Rows[row].HeaderCell.Value = homeGoals.ToString(CultureInfo.InvariantCulture);
            for (var awayGoals = 0; awayGoals <= maxGoals; awayGoals++)
            {
                var score = result.Scores.First(item => item.HomeGoals == homeGoals && item.AwayGoals == awayGoals);
                _matrixGrid.Rows[row].Cells[awayGoals].ToolTipText =
                    $"{score.ScoreLabel}: probabilidad {FormatPercent(score.Probability)}, odd justa {FormatOdds(score.Probability)}, odd con margen {FormatOddsWithMargin(score.Probability, result.BookmakerMarginPercent)}";
            }
        }

        _matrixGrid.TopLeftHeaderCell.Value = "L/V";
    }

    private void RenderSummary(ScoreMatrixResult result)
    {
        _summaryList.Items.Clear();
        AddSummary("Victoria local", result.HomeWinProbability);
        AddSummary("Empate", result.DrawProbability);
        AddSummary("Victoria visitante", result.AwayWinProbability);

        foreach (var line in result.OverUnderSummary)
        {
            AddSummary($"Over {line.Line:0.0}", line.Over);
            AddSummary($"Under {line.Line:0.0}", line.Under);
        }

        AddSummary("BTTS Sí", result.BothTeamsToScoreYes);
        AddSummary("BTTS No", result.BothTeamsToScoreNo);
        AddSummary("0-0", result.Scores.First(score => score.HomeGoals == 0 && score.AwayGoals == 0).Probability);
        AddSummary("1-1", result.Scores.First(score => score.HomeGoals == 1 && score.AwayGoals == 1).Probability);

        if (result.MostLikelyScore is not null)
        {
            AddSummary($"Más probable ({result.MostLikelyScore.ScoreLabel})", result.MostLikelyScore.Probability);
        }

        AddSummary("Probabilidad cubierta por la grilla", result.MatrixProbabilityMass);
        AddSummary("Probabilidad fuera de la grilla", result.ResidualProbability);
    }

    private void AddSummary(string name, double probability)
    {
        var item = new ListViewItem(name);
        item.SubItems.Add(FormatPercent(probability));
        item.SubItems.Add(FormatOdds(probability));
        item.SubItems.Add(FormatOddsWithMargin(probability, _lastResult?.BookmakerMarginPercent ?? 0));
        _summaryList.Items.Add(item);
    }

    private void ExportCsv()
    {
        if (_lastResult is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = "score-matrix.csv",
            AddExtension = true,
            DefaultExt = "csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, _csvExporter.Export(_lastResult));
    }

    private void ShowHelp()
    {
        using var form = new HelpForm();
        form.ShowDialog(this);
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

    private static string FormatPercent(double probability) => $"{probability * 100:0.00}%";
    private static string FormatOdds(double probability) => probability <= 0 ? "-" : $"{1.0 / probability:0.00}";
    private static string FormatOddsWithMargin(double probability, double marginPercent)
        => probability <= 0 ? "-" : $"{(1.0 / probability) / (1.0 + marginPercent / 100.0):0.00}";
}
