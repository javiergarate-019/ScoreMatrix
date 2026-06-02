using System.Globalization;
using ScoreMatrix.Application;
using ScoreMatrix.Domain;

namespace ScoreMatrix.WinForms;

public sealed class Form1 : Form
{
    private readonly ScoreMatrixCalculator _calculator = new(new OddsConverter(), new LambdaOptimizer());
    private readonly CsvExporter _csvExporter = new();

    private readonly TextBox _homeTeamText = new() { Text = "Local" };
    private readonly TextBox _awayTeamText = new() { Text = "Visitante" };
    private readonly RadioButton _oddsModeRadio = new() { Text = "Odds 1X2", Checked = true, AutoSize = true };
    private readonly RadioButton _lambdaModeRadio = new() { Text = "Goles esperados", AutoSize = true };
    private readonly NumericUpDown _homeOddsInput = CreateNumeric(2.10m, 1.01m, 100m, 2);
    private readonly NumericUpDown _drawOddsInput = CreateNumeric(3.25m, 1.01m, 100m, 2);
    private readonly NumericUpDown _awayOddsInput = CreateNumeric(3.60m, 1.01m, 100m, 2);
    private readonly NumericUpDown _homeLambdaInput = CreateNumeric(1.40m, 0.01m, 6m, 2);
    private readonly NumericUpDown _awayLambdaInput = CreateNumeric(1.10m, 0.01m, 6m, 2);
    private readonly NumericUpDown _maxGoalsInput = CreateNumeric(6m, 3m, 15m, 0);
    private readonly ComboBox _modelCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _rhoInput = CreateNumeric(-0.05m, -0.25m, 0.25m, 3, 0.005m);
    private readonly ComboBox _displayCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _lambdaLabel = new() { AutoSize = true };
    private readonly DataGridView _matrixGrid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, RowHeadersWidth = 72 };
    private readonly ListView _summaryList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };

    private ScoreMatrixResult? _lastResult;

    public Form1()
    {
        Text = "ScoreMatrix";
        MinimumSize = new Size(1120, 720);
        StartPosition = FormStartPosition.CenterScreen;

        _modelCombo.Items.AddRange(["Poisson", "Dixon-Coles"]);
        _modelCombo.SelectedIndex = 0;
        _displayCombo.Items.AddRange(["Probabilidades", "Odds justas"]);
        _displayCombo.SelectedIndex = 0;

        BuildLayout();
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));

        root.Controls.Add(BuildInputPanel(), 0, 0);
        root.Controls.Add(_matrixGrid, 0, 1);
        root.Controls.Add(BuildSummaryPanel(), 0, 2);
        Controls.Add(root);

        _matrixGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _matrixGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _matrixGrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _matrixGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        _summaryList.Columns.Add("Mercado", 240);
        _summaryList.Columns.Add("Probabilidad", 140);
        _summaryList.Columns.Add("Odd justa", 120);
    }

    private Control BuildInputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 4
        };

        for (var i = 0; i < 8; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        AddField(panel, "Equipo local", _homeTeamText, 0, 0);
        AddField(panel, "Equipo visitante", _awayTeamText, 2, 0);
        panel.Controls.Add(_oddsModeRadio, 4, 0);
        panel.Controls.Add(_lambdaModeRadio, 5, 0);

        AddField(panel, "Odd local", _homeOddsInput, 0, 1);
        AddField(panel, "Odd empate", _drawOddsInput, 2, 1);
        AddField(panel, "Odd visitante", _awayOddsInput, 4, 1);

        AddField(panel, "Lambda local", _homeLambdaInput, 0, 2);
        AddField(panel, "Lambda visitante", _awayLambdaInput, 2, 2);
        AddField(panel, "Max goles", _maxGoalsInput, 4, 2);
        AddField(panel, "Modelo", _modelCombo, 6, 2);

        AddField(panel, "Rho", _rhoInput, 0, 3);
        AddField(panel, "Vista", _displayCombo, 2, 3);

        var calculateButton = new Button { Text = "Calcular", Dock = DockStyle.Fill };
        calculateButton.Click += (_, _) => CalculateAndRender();
        panel.Controls.Add(calculateButton, 4, 3);

        var exportButton = new Button { Text = "Exportar CSV", Dock = DockStyle.Fill };
        exportButton.Click += (_, _) => ExportCsv();
        panel.Controls.Add(exportButton, 5, 3);

        _lambdaLabel.Dock = DockStyle.Fill;
        _lambdaLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(_lambdaLabel, 6, 3);
        panel.SetColumnSpan(_lambdaLabel, 2);

        return panel;
    }

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
        var wrapper = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        wrapper.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        control.Dock = DockStyle.Fill;
        wrapper.Controls.Add(control, 1, 0);
        panel.Controls.Add(wrapper, column, row);
        panel.SetColumnSpan(wrapper, 2);
    }

    private void WireEvents()
    {
        _oddsModeRadio.CheckedChanged += (_, _) => UpdateInputState();
        _modelCombo.SelectedIndexChanged += (_, _) => UpdateInputState();
        _displayCombo.SelectedIndexChanged += (_, _) => RenderResult();
        UpdateInputState();
    }

    private void UpdateInputState()
    {
        var oddsMode = _oddsModeRadio.Checked;
        _homeOddsInput.Enabled = oddsMode;
        _drawOddsInput.Enabled = oddsMode;
        _awayOddsInput.Enabled = oddsMode;
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
            HomeExpectedGoals = (double)_homeLambdaInput.Value,
            AwayExpectedGoals = (double)_awayLambdaInput.Value,
            ModelType = _modelCombo.SelectedIndex == 1 ? ScoreModelType.DixonColes : ScoreModelType.Poisson,
            MaxGoals = (int)_maxGoalsInput.Value,
            DisplayMode = _displayCombo.SelectedIndex == 1 ? DisplayMode.FairOdds : DisplayMode.Probabilities,
            DixonColesRho = (double)_rhoInput.Value
        };

    private void RenderResult()
    {
        if (_lastResult is null)
        {
            return;
        }

        _lambdaLabel.Text = $"λ local {_lastResult.LambdaHome:0.000}  |  λ visitante {_lastResult.LambdaAway:0.000}";
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
                values[awayGoals] = _displayCombo.SelectedIndex == 1 ? FormatOdds(score.Probability) : FormatPercent(score.Probability);
            }

            var row = _matrixGrid.Rows.Add(values);
            _matrixGrid.Rows[row].HeaderCell.Value = homeGoals.ToString(CultureInfo.InvariantCulture);
        }

        _matrixGrid.TopLeftHeaderCell.Value = $"{result.HomeTeamName} \\ {result.AwayTeamName}";
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

        AddSummary("Masa matriz mostrada", result.MatrixProbabilityMass);
        AddSummary("Residual fuera de rango", result.ResidualProbability);
    }

    private void AddSummary(string name, double probability)
    {
        var item = new ListViewItem(name);
        item.SubItems.Add(FormatPercent(probability));
        item.SubItems.Add(FormatOdds(probability));
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

    private static NumericUpDown CreateNumeric(decimal value, decimal min, decimal max, int decimals, decimal increment = 0.01m)
        => new()
        {
            Value = value,
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimals,
            Increment = increment,
            ThousandsSeparator = false
        };

    private static string FormatPercent(double probability) => $"{probability * 100:0.00}%";
    private static string FormatOdds(double probability) => probability <= 0 ? "-" : $"{1.0 / probability:0.00}";
}
