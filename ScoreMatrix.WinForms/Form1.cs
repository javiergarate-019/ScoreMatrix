using System.Globalization;
using ScoreMatrix.Application;
using ScoreMatrix.Domain;

namespace ScoreMatrix.WinForms;

public sealed class Form1 : Form
{
    private readonly ScoreMatrixCalculator _calculator = new(new OddsConverter(), new LambdaOptimizer(), new PencaEvaluator());
    private readonly CsvExporter _csvExporter = new();
    private readonly OddsApiClient _oddsApiClient = new();
    private readonly AppSettings _settings = AppSettings.Load();
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
    private readonly CheckBox _useOverUnderInput = new() { Text = "Usar O/U", AutoSize = true };
    private readonly NumericUpDown _overUnderLineInput = CreateNumeric(2.5m, 0.5m, 6m, 1, 0.5m);
    private readonly NumericUpDown _overOddsInput = CreateNumeric(1.90m, 1.01m, 100m, 2);
    private readonly NumericUpDown _underOddsInput = CreateNumeric(1.90m, 1.01m, 100m, 2);
    private readonly CheckBox _useBothTeamsToScoreInput = new() { Text = "Usar BTTS", AutoSize = true };
    private readonly NumericUpDown _bothTeamsToScoreYesOddsInput = CreateNumeric(1.90m, 1.01m, 100m, 2);
    private readonly NumericUpDown _bothTeamsToScoreNoOddsInput = CreateNumeric(1.90m, 1.01m, 100m, 2);
    private readonly TextBox _oddsApiKeyInput = new() { UseSystemPasswordChar = true };
    private readonly ComboBox _oddsApiRegionCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _worldCupMatchCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _fetchWorldCupEventsButton = new() { Text = "Buscar partidos", Dock = DockStyle.Fill };
    private readonly Button _fetchWorldCupOddsButton = new() { Text = "Traer cuotas", Dock = DockStyle.Fill };
    private readonly NumericUpDown _homeLambdaInput = CreateNumeric(1.40m, 0.01m, 6m, 2);
    private readonly NumericUpDown _awayLambdaInput = CreateNumeric(1.10m, 0.01m, 6m, 2);
    private readonly NumericUpDown _maxGoalsInput = CreateNumeric(6m, 3m, 15m, 0);
    private readonly ComboBox _modelCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _rhoInput = CreateNumeric(-0.05m, -0.25m, 0.25m, 3, 0.005m);
    private readonly CheckBox _autoCalibrateRhoInput = new() { Text = "Auto-calibrar", AutoSize = true };
    private readonly ComboBox _devigCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _marginInput = CreateNumeric(5m, 0m, 50m, 2, 0.25m);
    private readonly ComboBox _displayCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _pencaExactPointsInput = CreateNumeric(7m, 0m, 20m, 0);
    private readonly NumericUpDown _pencaResultPointsInput = CreateNumeric(2m, 0m, 20m, 0);
    private readonly NumericUpDown _pencaGoalsPointsInput = CreateNumeric(1m, 0m, 20m, 0);
    private readonly Label _lambdaLabel = new() { AutoSize = true };
    private readonly DataGridView _matrixGrid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, RowHeadersWidth = 72 };
    private readonly ListView _summaryList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
    private readonly ListView _pencaRankingList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };

    private IReadOnlyList<OddsApiEvent> _worldCupEvents = [];
    private ScoreMatrixResult? _lastResult;
    private bool _suppressAutoCalculate;

    public Form1()
    {
        Text = "ScoreMatrix";
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? Icon;
        MinimumSize = new Size(1180, 900);
        Size = new Size(1320, 940);
        StartPosition = FormStartPosition.CenterScreen;

        _modelCombo.Items.AddRange(["Poisson", "Dixon-Coles", "Bivariado Poisson", "Binomial Negativa"]);
        _modelCombo.SelectedIndex = 1;  // Dixon-Coles: mejor que Poisson puro para marcadores bajos
        _displayCombo.Items.AddRange(["Probabilidades", "Odds justas", "Odds con margen"]);
        _displayCombo.SelectedIndex = 0;
        _devigCombo.Items.AddRange(["Proporcional", "Power", "Shin", "Aditivo"]);
        _devigCombo.SelectedIndex = 2;  // Shin: corrige el sesgo favorito-longshot mejor que el proporcional
        _oddsApiRegionCombo.Items.AddRange(["eu", "uk", "us", "au"]);
        _oddsApiKeyInput.Text = _settings.OddsApiKey;
        _oddsApiRegionCombo.SelectedItem = _oddsApiRegionCombo.Items.Contains(_settings.OddsApiRegion)
            ? _settings.OddsApiRegion
            : "eu";

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
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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

        _summaryList.Columns.Add("Mercado", 220);
        _summaryList.Columns.Add("Probabilidad", 120);
        _summaryList.Columns.Add("Odd justa", 100);
        _summaryList.Columns.Add("Odd con margen", 120);

        _pencaRankingList.Columns.Add("Marcador", 90);
        _pencaRankingList.Columns.Add("Puntaje esperado", 130);
        _pencaRankingList.Columns.Add("Prob. exacta", 110);
    }

    private Control BuildInputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 10,
            RowCount = 7
        };

        for (var i = 0; i < 10; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

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
        calibrationPanel.Controls.Add(_useOverUnderInput, 0, 0);
        calibrationPanel.SetColumnSpan(_useOverUnderInput, 2);
        _useOverUnderInput.Anchor = AnchorStyles.Left;
        AddField(calibrationPanel, "Linea O/U", _overUnderLineInput, 2, 0);
        AddField(calibrationPanel, "Odd Over", _overOddsInput, 4, 0);
        AddField(calibrationPanel, "Odd Under", _underOddsInput, 6, 0);
        calibrationPanel.Controls.Add(_useBothTeamsToScoreInput, 0, 1);
        calibrationPanel.SetColumnSpan(_useBothTeamsToScoreInput, 2);
        _useBothTeamsToScoreInput.Anchor = AnchorStyles.Left;
        AddField(calibrationPanel, "Odd BTTS Si", _bothTeamsToScoreYesOddsInput, 2, 1);
        AddField(calibrationPanel, "Odd BTTS No", _bothTeamsToScoreNoOddsInput, 4, 1);
        calibrationGroup.Controls.Add(calibrationPanel);
        panel.Controls.Add(calibrationGroup, 0, 2);
        panel.SetColumnSpan(calibrationGroup, 10);

        var worldCupGroup = CreateGroupBox("Mundial 2026 (The Odds API)", topPadding: 10);
        var worldCupPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 64, ColumnCount = 10, RowCount = 1, Margin = new Padding(0) };
        for (var i = 0; i < 10; i++)
        {
            worldCupPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f));
        }

        AddField(worldCupPanel, "API key", _oddsApiKeyInput, 0, 0);
        AddField(worldCupPanel, "Region", _oddsApiRegionCombo, 2, 0);
        AddField(worldCupPanel, "Partido", _worldCupMatchCombo, 4, 0);

        var fetchEventsWrapper = CreateButtonField("Buscar", _fetchWorldCupEventsButton);
        worldCupPanel.Controls.Add(fetchEventsWrapper, 6, 0);
        worldCupPanel.SetColumnSpan(fetchEventsWrapper, 2);

        var fetchOddsWrapper = CreateButtonField("Importar", _fetchWorldCupOddsButton);
        worldCupPanel.Controls.Add(fetchOddsWrapper, 8, 0);
        worldCupPanel.SetColumnSpan(fetchOddsWrapper, 2);

        worldCupGroup.Controls.Add(worldCupPanel);
        panel.Controls.Add(worldCupGroup, 0, 3);
        panel.SetColumnSpan(worldCupGroup, 10);

        // Row 4: lambdas, max goles, modelo, rho + auto-calibrar
        AddField(panel, "Lambda local", _homeLambdaInput, 0, 4);
        AddField(panel, "Lambda visitante", _awayLambdaInput, 2, 4);
        AddField(panel, "Max goles", _maxGoalsInput, 4, 4);
        AddField(panel, "Modelo", _modelCombo, 6, 4);

        // Rho + auto-calibrar en la misma columna
        var rhoWrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(2, 0, 8, 4)
        };
        rhoWrapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        rhoWrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        rhoWrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        rhoWrapper.Controls.Add(new Label { Text = "Rho / Dep.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        _rhoInput.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        rhoWrapper.Controls.Add(_rhoInput, 0, 1);
        _autoCalibrateRhoInput.Anchor = AnchorStyles.Left;
        rhoWrapper.Controls.Add(_autoCalibrateRhoInput, 0, 2);
        panel.Controls.Add(rhoWrapper, 8, 4);
        panel.SetColumnSpan(rhoWrapper, 2);

        // Row 5: vista, margen, de-vig, penca
        AddField(panel, "Vista", _displayCombo, 0, 5);
        AddField(panel, "Margen %", _marginInput, 2, 5);
        AddField(panel, "De-vig", _devigCombo, 4, 5);
        AddField(panel, "Pts exacto", _pencaExactPointsInput, 6, 5);
        AddField(panel, "Pts resultado", _pencaResultPointsInput, 7, 5);
        AddField(panel, "Pts goles", _pencaGoalsPointsInput, 8, 5);

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(2, 4, 8, 4)
        };
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        var calculateButton = new Button { Text = "Calcular", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        calculateButton.Click += (_, _) => CalculateAndRender();
        buttonPanel.Controls.Add(calculateButton, 0, 0);

        var exportButton = new Button { Text = "Exportar CSV", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        exportButton.Click += (_, _) => ExportCsv();
        buttonPanel.Controls.Add(exportButton, 1, 0);

        var backtestButton = new Button { Text = "Backtest", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        backtestButton.Click += (_, _) => ShowBacktest();
        buttonPanel.Controls.Add(backtestButton, 2, 0);

        var helpButton = new Button { Text = "Ayuda", Dock = DockStyle.Fill, Margin = new Padding(0) };
        helpButton.Click += (_, _) => ShowHelp();
        buttonPanel.Controls.Add(helpButton, 3, 0);
        panel.Controls.Add(buttonPanel, 4, 6);
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
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 520
        };

        var summaryPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        summaryPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        summaryPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        summaryPanel.Controls.Add(new Label { Text = "Resumen agregado", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        summaryPanel.Controls.Add(_summaryList, 0, 1);

        var pencaPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        pencaPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        pencaPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pencaPanel.Controls.Add(new Label { Text = "Penca: a que conviene apostar", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        pencaPanel.Controls.Add(_pencaRankingList, 0, 1);

        split.Panel1.Controls.Add(summaryPanel);
        split.Panel2.Controls.Add(pencaPanel);
        return split;
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

    private static Control CreateButtonField(string label, Control control)
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
        return wrapper;
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
        SetTip(_useOverUnderInput, "Activa cuotas Over/Under para calibrar mejor el total esperado de goles.");
        SetTip(_overUnderLineInput, "Linea de goles totales usada para la calibracion Over/Under.");
        SetTip(_overOddsInput, "Cuota decimal para Over. Se usa solo si Usar O/U esta activado.");
        SetTip(_underOddsInput, "Cuota decimal para Under. Se usa solo si Usar O/U esta activado.");
        SetTip(_useBothTeamsToScoreInput, "Activa cuotas BTTS Si/No para calibrar si ambos equipos marcan.");
        SetTip(_bothTeamsToScoreYesOddsInput, "Cuota decimal para BTTS Si. Se usa solo si Usar BTTS esta activado.");
        SetTip(_bothTeamsToScoreNoOddsInput, "Cuota decimal para BTTS No. Se usa solo si Usar BTTS esta activado.");
        SetTip(_oddsApiKeyInput, "API key de The Odds API. Se guarda localmente en %APPDATA%\\ScoreMatrix\\settings.json.");
        SetTip(_oddsApiRegionCombo, "Region de bookmakers a consultar: eu, uk, us o au.");
        SetTip(_worldCupMatchCombo, "Partidos del Mundial 2026 disponibles en The Odds API.");
        SetTip(_fetchWorldCupEventsButton, "Carga la lista de partidos del Mundial 2026. Este endpoint no consume cuota.");
        SetTip(_fetchWorldCupOddsButton, "Importa cuotas 1X2 de consenso sin margen para el partido seleccionado.");
        SetTip(_homeLambdaInput, "Goles esperados del local. Se usa solo en modo Goles esperados.");
        SetTip(_awayLambdaInput, "Goles esperados del visitante. Se usa solo en modo Goles esperados.");
        SetTip(_maxGoalsInput, "Mayor marcador que se muestra en la matriz. Puede estar entre 3 y 15.");
        SetTip(_modelCombo, "Modelo matematico: Poisson (independiente), Dixon-Coles (ajusta marcadores bajos), Bivariado Poisson (correlacion entre goles), Binomial Negativa (sobredispersion).");
        SetTip(_rhoInput, "Parametro de dependencia del modelo (rho para Dixon-Coles, lambda3 para Bivariado). Se ignora si Auto-calibrar esta activo.");
        SetTip(_autoCalibrateRhoInput, "Cuando esta activo, el optimizador estima el parametro de dependencia desde O/U y BTTS. Requiere esos mercados habilitados y un modelo no-Poisson.");
        SetTip(_devigCombo, "Metodo para quitar el margen del bookmaker. Proporcional: estandar; Power: corrige el sesgo favorito-longshot; Shin: modelo de insiders; Aditivo: resta igual margen a todos los resultados.");
        SetTip(_marginInput, "Margen comercial que se aplica sobre las odds justas al usar la vista Odds con margen. Usa 0% si queres ver la cuota justa sin descuento.");
        SetTip(_displayCombo, "Probabilidades muestra el porcentaje estimado. Odds justas muestra 1 dividido por esa probabilidad. Odds con margen descuenta el margen indicado.");
        SetTip(_pencaExactPointsInput, "Puntos por resultado exacto. Default Penca Grupo RAS: 7.");
        SetTip(_pencaResultPointsInput, "Puntos por acertar ganador o empate. Default: 2.");
        SetTip(_pencaGoalsPointsInput, "Puntos por acertar goles de un equipo. Default: 1. Resultado + goles suma 3.");
        SetTip(_matrixGrid, "Cada celda muestra la probabilidad estimada del marcador exacto o su cuota justa, segun la vista elegida.");
        SetTip(_summaryList, "Probabilidad es la chance estimada del mercado agregado. Odd justa no incluye margen. Odd con margen descuenta el margen indicado.");
        SetTip(_pencaRankingList, "Ranking de marcadores ordenados por puntaje esperado segun las reglas de la penca.");
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
        _devigCombo.SelectedIndexChanged += (_, _) => CalculateAndRender();
        _autoCalibrateRhoInput.CheckedChanged += (_, _) =>
        {
            UpdateInputState();
            CalculateAndRender();
        };
        _displayCombo.SelectedIndexChanged += (_, _) => RenderResult();
        _marginInput.ValueChanged += (_, _) => UpdateMarginDisplay();
        _homeOddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _drawOddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _awayOddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _useOverUnderInput.CheckedChanged += (_, _) =>
        {
            UpdateInputState();
            CalculateAndRender();
        };
        _overUnderLineInput.ValueChanged += (_, _) => CalculateAndRender();
        _overOddsInput.ValueChanged += (_, _) => CalculateAndRender();
        _underOddsInput.ValueChanged += (_, _) => CalculateAndRender();
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
        _pencaExactPointsInput.ValueChanged += (_, _) => CalculateAndRender();
        _pencaResultPointsInput.ValueChanged += (_, _) => CalculateAndRender();
        _pencaGoalsPointsInput.ValueChanged += (_, _) => CalculateAndRender();
        _oddsApiKeyInput.Leave += (_, _) => SaveSettings();
        _oddsApiRegionCombo.SelectedIndexChanged += (_, _) => SaveSettings();
        _fetchWorldCupEventsButton.Click += async (_, _) => await FetchWorldCupEventsAsync();
        _fetchWorldCupOddsButton.Click += async (_, _) => await FetchWorldCupOddsAsync();
        UpdateInputState();
    }

    private void SaveSettings()
    {
        _settings.OddsApiKey = _oddsApiKeyInput.Text.Trim();
        _settings.OddsApiRegion = _oddsApiRegionCombo.SelectedItem?.ToString() ?? "eu";
        _settings.Save();
    }

    private async Task FetchWorldCupEventsAsync()
    {
        SaveSettings();

        try
        {
            UseWaitCursor = true;
            _fetchWorldCupEventsButton.Enabled = false;
            _fetchWorldCupOddsButton.Enabled = false;

            _worldCupEvents = await _oddsApiClient.GetWorldCupEventsAsync(
                _oddsApiKeyInput.Text.Trim(),
                _oddsApiRegionCombo.SelectedItem?.ToString() ?? "eu");

            _worldCupMatchCombo.Items.Clear();
            foreach (var match in _worldCupEvents)
            {
                var localTime = match.CommenceTime.ToLocalTime();
                _worldCupMatchCombo.Items.Add($"{localTime:dd/MM HH:mm} - {match.HomeTeam} vs {match.AwayTeam}");
            }

            if (_worldCupMatchCombo.Items.Count > 0)
            {
                _worldCupMatchCombo.SelectedIndex = 0;
            }

            MessageBox.Show(
                this,
                $"Se encontraron {_worldCupEvents.Count} partidos del Mundial 2026.",
                "The Odds API",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (ScoreMatrixValidationException ex)
        {
            MessageBox.Show(this, ex.Message, "Validacion", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _fetchWorldCupEventsButton.Enabled = true;
            _fetchWorldCupOddsButton.Enabled = true;
        }
    }

    private async Task FetchWorldCupOddsAsync()
    {
        SaveSettings();

        if (_worldCupMatchCombo.SelectedIndex < 0 || _worldCupMatchCombo.SelectedIndex >= _worldCupEvents.Count)
        {
            MessageBox.Show(this, "Primero busca y selecciona un partido del Mundial 2026.", "Validacion", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedEvent = _worldCupEvents[_worldCupMatchCombo.SelectedIndex];

        try
        {
            UseWaitCursor = true;
            _fetchWorldCupEventsButton.Enabled = false;
            _fetchWorldCupOddsButton.Enabled = false;

            var consensus = await _oddsApiClient.GetConsensusOddsAsync(
                _oddsApiKeyInput.Text.Trim(),
                _oddsApiRegionCombo.SelectedItem?.ToString() ?? "eu",
                selectedEvent.Id);

            _suppressAutoCalculate = true;
            try
            {
                _homeTeamText.Text = consensus.HomeTeam;
                _awayTeamText.Text = consensus.AwayTeam;
                _homeOddsInput.Value = ClampDecimal((decimal)consensus.HomeOdds, _homeOddsInput.Minimum, _homeOddsInput.Maximum);
                _drawOddsInput.Value = ClampDecimal((decimal)consensus.DrawOdds, _drawOddsInput.Minimum, _drawOddsInput.Maximum);
                _awayOddsInput.Value = ClampDecimal((decimal)consensus.AwayOdds, _awayOddsInput.Minimum, _awayOddsInput.Maximum);
                _oddsModeRadio.Checked = true;

                if (consensus.OverUnderLine is not null &&
                    consensus.OverOdds is not null &&
                    consensus.UnderOdds is not null)
                {
                    _useOverUnderInput.Checked = true;
                    _overUnderLineInput.Value = ClampDecimal((decimal)consensus.OverUnderLine.Value, _overUnderLineInput.Minimum, _overUnderLineInput.Maximum);
                    _overOddsInput.Value = ClampDecimal((decimal)consensus.OverOdds.Value, _overOddsInput.Minimum, _overOddsInput.Maximum);
                    _underOddsInput.Value = ClampDecimal((decimal)consensus.UnderOdds.Value, _underOddsInput.Minimum, _underOddsInput.Maximum);
                }
                else
                {
                    _useOverUnderInput.Checked = false;
                    _overUnderLineInput.Value = 2.5m;
                    _overOddsInput.Value = 1.90m;
                    _underOddsInput.Value = 1.90m;
                }

                if (consensus.BttsYesOdds is not null && consensus.BttsNoOdds is not null)
                {
                    _useBothTeamsToScoreInput.Checked = true;
                    _bothTeamsToScoreYesOddsInput.Value = ClampDecimal((decimal)consensus.BttsYesOdds.Value, _bothTeamsToScoreYesOddsInput.Minimum, _bothTeamsToScoreYesOddsInput.Maximum);
                    _bothTeamsToScoreNoOddsInput.Value = ClampDecimal((decimal)consensus.BttsNoOdds.Value, _bothTeamsToScoreNoOddsInput.Minimum, _bothTeamsToScoreNoOddsInput.Maximum);
                }
                else
                {
                    _useBothTeamsToScoreInput.Checked = false;
                    _bothTeamsToScoreYesOddsInput.Value = 1.90m;
                    _bothTeamsToScoreNoOddsInput.Value = 1.90m;
                }
            }
            finally
            {
                _suppressAutoCalculate = false;
            }

            CalculateAndRender();

            var quotaMessage = string.IsNullOrWhiteSpace(consensus.RequestsRemaining)
                ? string.Empty
                : $" Cuota restante: {consensus.RequestsRemaining}.";

            MessageBox.Show(
                this,
                $"{BuildImportSummary(consensus)}.{quotaMessage}",
                "The Odds API",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (ScoreMatrixValidationException ex)
        {
            MessageBox.Show(this, ex.Message, "Validacion", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _fetchWorldCupEventsButton.Enabled = true;
            _fetchWorldCupOddsButton.Enabled = true;
        }
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
        _useOverUnderInput.Enabled = oddsMode;
        _overUnderLineInput.Enabled = oddsMode && _useOverUnderInput.Checked;
        _overOddsInput.Enabled = oddsMode && _useOverUnderInput.Checked;
        _underOddsInput.Enabled = oddsMode && _useOverUnderInput.Checked;
        _useBothTeamsToScoreInput.Enabled = oddsMode;
        _bothTeamsToScoreYesOddsInput.Enabled = oddsMode && _useBothTeamsToScoreInput.Checked;
        _bothTeamsToScoreNoOddsInput.Enabled = oddsMode && _useBothTeamsToScoreInput.Checked;
        _devigCombo.Enabled = oddsMode;
        _homeLambdaInput.Enabled = !oddsMode;
        _awayLambdaInput.Enabled = !oddsMode;

        var modelIdx = _modelCombo.SelectedIndex;
        var hasDependenceParam = modelIdx is 1 or 2; // Dixon-Coles or Bivariate Poisson
        var canAutoCalibrate = hasDependenceParam && (_useOverUnderInput.Checked || _useBothTeamsToScoreInput.Checked) && oddsMode;

        _autoCalibrateRhoInput.Enabled = canAutoCalibrate;
        if (!canAutoCalibrate) _autoCalibrateRhoInput.Checked = false;
        _rhoInput.Enabled = (modelIdx is 1 or 2 or 3) && !_autoCalibrateRhoInput.Checked;
    }

    private void CalculateAndRender()
    {
        if (_suppressAutoCalculate)
        {
            return;
        }

        try
        {
            _lastResult = _calculator.Calculate(ReadInput());
            RenderResult();
        }
        catch (ScoreMatrixValidationException ex)
        {
            MessageBox.Show(this, ex.Message, "Validacion", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            UseOverUnderOdds = _useOverUnderInput.Checked,
            OverUnderLine = (double)_overUnderLineInput.Value,
            OverOdds = (double)_overOddsInput.Value,
            UnderOdds = (double)_underOddsInput.Value,
            UseBothTeamsToScoreOdds = _useBothTeamsToScoreInput.Checked,
            BothTeamsToScoreYesOdds = (double)_bothTeamsToScoreYesOddsInput.Value,
            BothTeamsToScoreNoOdds = (double)_bothTeamsToScoreNoOddsInput.Value,
            HomeExpectedGoals = (double)_homeLambdaInput.Value,
            AwayExpectedGoals = (double)_awayLambdaInput.Value,
            ModelType = _modelCombo.SelectedIndex switch
            {
                1 => ScoreModelType.DixonColes,
                2 => ScoreModelType.BivariatePoisson,
                3 => ScoreModelType.NegativeBinomial,
                _ => ScoreModelType.Poisson
            },
            MaxGoals = (int)_maxGoalsInput.Value,
            DisplayMode = _displayCombo.SelectedIndex switch
            {
                1 => DisplayMode.FairOdds,
                2 => DisplayMode.OddsWithMargin,
                _ => DisplayMode.Probabilities
            },
            BookmakerMarginPercent = (double)_marginInput.Value,
            DixonColesRho = (double)_rhoInput.Value,
            AutoCalibrateDependence = _autoCalibrateRhoInput.Checked,
            DevigMethod = _devigCombo.SelectedIndex switch
            {
                1 => DevigMethod.Power,
                2 => DevigMethod.Shin,
                3 => DevigMethod.Additive,
                _ => DevigMethod.Proportional
            },
            PencaScoringRules = new PencaScoringRules(
                (int)_pencaExactPointsInput.Value,
                (int)_pencaResultPointsInput.Value,
                (int)_pencaGoalsPointsInput.Value)
        };

    private void RenderResult()
    {
        if (_lastResult is null)
        {
            return;
        }

        var depLabel = _lastResult.EstimatedDependence != 0
            ? $" | dep. {_lastResult.EstimatedDependence:0.000}"
            : string.Empty;
        _lambdaLabel.Text = $"Lambdas usados: local {_lastResult.LambdaHome:0.000} | visitante {_lastResult.LambdaAway:0.000}{depLabel}";
        RenderMatrix(_lastResult);
        RenderSummary(_lastResult);
        RenderPencaRanking(_lastResult);
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

        AddSummary("BTTS Si", result.BothTeamsToScoreYes);
        AddSummary("BTTS No", result.BothTeamsToScoreNo);
        AddSummary("0-0", result.Scores.First(score => score.HomeGoals == 0 && score.AwayGoals == 0).Probability);
        AddSummary("1-1", result.Scores.First(score => score.HomeGoals == 1 && score.AwayGoals == 1).Probability);

        if (result.MostLikelyScore is not null)
        {
            AddSummary($"Mas probable ({result.MostLikelyScore.ScoreLabel})", result.MostLikelyScore.Probability);
        }

        AddSummary("Probabilidad cubierta por la grilla", result.MatrixProbabilityMass);
        AddSummary("Probabilidad fuera de la grilla", result.ResidualProbability);
    }

    private void RenderPencaRanking(ScoreMatrixResult result)
    {
        _pencaRankingList.Items.Clear();

        foreach (var recommendation in result.PencaRanking)
        {
            var item = new ListViewItem(recommendation.ScoreLabel);
            item.SubItems.Add($"{recommendation.ExpectedPoints:0.###}");
            item.SubItems.Add(FormatPercent(recommendation.ExactProbability));
            _pencaRankingList.Items.Add(item);
        }
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

    private void ShowBacktest()
    {
        using var form = new BacktestForm(_calculator, _csvExporter);
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

    private static string BuildImportSummary(OddsApiConsensusOdds consensus)
    {
        var parts = new List<string> { $"1X2 ({consensus.BookmakerCount} bookmakers)" };

        if (consensus.OverUnderLine is not null && consensus.OverUnderBookmakerCount is not null)
        {
            parts.Add($"O/U {consensus.OverUnderLine:0.0#} ({consensus.OverUnderBookmakerCount} bookmakers)");
        }

        if (consensus.BttsBookmakerCount is not null)
        {
            parts.Add($"BTTS ({consensus.BttsBookmakerCount} bookmakers)");
        }

        return $"Importado: {string.Join(", ", parts)}";
    }

    private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        => Math.Min(max, Math.Max(min, value));

    private static string FormatPercent(double probability) => $"{probability * 100:0.00}%";
    private static string FormatOdds(double probability) => probability <= 0 ? "-" : $"{1.0 / probability:0.00}";
    private static string FormatOddsWithMargin(double probability, double marginPercent)
        => probability <= 0 ? "-" : $"{(1.0 / probability) / (1.0 + marginPercent / 100.0):0.00}";
}
