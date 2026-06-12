namespace ScoreMatrix.WinForms;

public sealed class HelpForm : Form
{
    public HelpForm()
    {
        Text = "Ayuda - ScoreMatrix";
        BackColor = Color.White;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 640);
        Size = new Size(860, 720);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.Controls.Add(BuildHeader(), 0, 0);

        var text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(32, 32, 32),
            Font = new Font("Segoe UI", 9),
            DetectUrls = false,
            Text = BuildHelpText()
        };
        text.SelectAll();
        text.SelectionIndent = 8;
        text.SelectionRightIndent = 8;
        text.SelectionBackColor = Color.White;
        text.SelectionLength = 0;

        var closeButton = new Button
        {
            Text = "Cerrar",
            Anchor = AnchorStyles.Right,
            Width = 110,
            Height = 32
        };
        closeButton.Click += (_, _) => Close();

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var copyright = new LinkLabel
        {
            Text = $"Copyright (c) {DateTime.Now.Year} Javier Garate Copello. Bajo Licencia MIT.",
            AutoSize = true,
            ForeColor = Color.FromArgb(96, 96, 96),
            LinkColor = Color.FromArgb(24, 98, 110),
            ActiveLinkColor = Color.FromArgb(18, 72, 82),
            VisitedLinkColor = Color.FromArgb(24, 98, 110),
            Font = new Font("Segoe UI", 8),
            Location = new Point(0, 4)
        };
        copyright.Links.Add(copyright.Text.IndexOf("Licencia MIT", StringComparison.Ordinal), "Licencia MIT".Length, "https://opensource.org/license/mit");
        copyright.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is string url)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
        };
        footer.Controls.Add(copyright);
        footer.Controls.Add(closeButton);
        closeButton.Location = new Point(footer.Width - closeButton.Width, 6);
        footer.Resize += (_, _) => closeButton.Location = new Point(footer.Width - closeButton.Width, 6);

        root.Controls.Add(text, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
    }

    private static Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var icon = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 12, 16)
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ScoreMatrixIconLarge.png");
        if (File.Exists(iconPath))
        {
            using var image = Image.FromFile(iconPath);
            icon.Image = new Bitmap(image);
        }

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White
        };
        titlePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        titlePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        titlePanel.Controls.Add(new Label
        {
            Text = "ScoreMatrix",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 58, 62)
        }, 0, 0);
        titlePanel.Controls.Add(new Label
        {
            Text = "Ayuda y conceptos del modelo",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(96, 96, 96)
        }, 0, 1);

        panel.Controls.Add(icon, 0, 0);
        panel.Controls.Add(titlePanel, 1, 0);
        return panel;
    }

    private static string BuildHelpText()
        => """
        ScoreMatrix calcula probabilidades de marcadores exactos para futbol a partir de un modelo matematico.


        1. MODOS DE ENTRADA

        Odds 1X2
        Ingresas las cuotas decimales de victoria local, empate y victoria visitante. La app infiere los goles esperados de cada equipo.

        Usar O/U
        En modo Odds 1X2 podes activar cuotas Over/Under con una linea configurable (default 2.5). Esto ayuda a calibrar mejor el total esperado de goles.

        Usar BTTS
        En modo Odds 1X2 podes activar cuotas Both Teams To Score Si/No. Esto ayuda a calibrar la probabilidad de que ambos equipos conviertan.

        Goles esperados
        Ingresas directamente Lambda local y Lambda visitante. En este modo no se usan las odds 1X2 ni se optimizan lambdas.


        2. METODOS DE DE-VIG

        El de-vig convierte cuotas del bookmaker en probabilidades sin margen. ScoreMatrix ofrece cuatro metodos:

        Proporcional (default)
        Divide la probabilidad implicita de cada resultado por la suma total. Rapido y estandar.

        Power (multiplicativo)
        Busca un exponente k > 1 tal que la suma de (1/odd)^k sea exactamente 1. Corrige mejor el sesgo favorito-longshot que el metodo proporcional.

        Shin (modelo de insiders)
        Aplica el modelo de Shin (1992), que asume una fraccion z de apuestas informadas. Produce las estimaciones mas precisas en partidos con marcados favoritos. El parametro z se estima internamente por biseccion.

        Aditivo
        Resta una fraccion igual del margen a cada resultado. Preserva las diferencias absolutas entre probabilidades.


        3. DE PROBABILIDADES 1X2 A LAMBDAS

        Lambda local y Lambda visitante representan los goles esperados de cada equipo.

        Para obtenerlos desde odds 1X2, la app usa un optimizador Nelder-Mead con objetivo KL divergence. El optimizador busca lambdas que produzcan distribuciones 1X2 lo mas cercanas posible a las del mercado.

        Objetivo (KL divergence):
            error = KL(mercado || modelo) para cada mercado activo (1X2, O/U, BTTS)

        El KL divergence penaliza mejor los desvios en probabilidades que el error cuadratico clasico.

        Si activas Usar O/U o Usar BTTS, esos mercados tambien entran al objetivo del optimizador.


        4. AUTO-CALIBRACION DE DEPENDENCIA (rho / lambda3)

        Con los modelos Dixon-Coles o Bivariado Poisson, si activas Usar O/U o Usar BTTS podes marcar
        "Auto-calibrar". En ese modo, el optimizador Nelder-Mead ajusta tres parametros en lugar de dos:
        lambda local, lambda visitante y el parametro de dependencia.

        Esto le permite al modelo encontrar el rho (o lambda3) que mejor encaja los mercados disponibles,
        en vez de usar el valor fijo que ingresas manualmente.

        El valor estimado se muestra junto a los lambdas al calcular.


        5. MODELO POISSON

        El modelo base asume:

            goles local ~ Poisson(lambda local)
            goles visitante ~ Poisson(lambda visitante)

        La probabilidad de un marcador exacto se calcula asi:

            P(i-j) = Poisson(i, lambda local) * Poisson(j, lambda visitante)


        6. MODELO DIXON-COLES

        Dixon-Coles parte de Poisson, pero ajusta marcadores bajos (0-0, 1-0, 0-1, 1-1) con un parametro rho.

        rho = 0: igual que Poisson.
        rho < 0: sube 0-0 y 1-1, baja 1-0 y 0-1.
        rho > 0: baja 0-0 y 1-1, sube 1-0 y 0-1.


        7. MODELO BIVARIADO POISSON

        Captura correlacion positiva entre los goles de ambos equipos usando un parametro de covarianza lambda3.
        A diferencia de Dixon-Coles, afecta toda la distribucion, no solo marcadores bajos.

        Basado en Maher (1982) y Karlis & Ntzoufras (2003).

        El parametro de dependencia en [-0.25, 0.25] escala la covarianza relativa a las lambdas.
        Valor 0: equivalente a Poisson independiente.
        Valor positivo: mayor correlacion (mas goles en el mismo partido).


        8. MODELO BINOMIAL NEGATIVA

        Usa distribuciones Binomial Negativa en lugar de Poisson para cada equipo (independientes).
        La varianza supera a la media (sobredispersion), lo que produce colas mas gruesas y mas 0s comparado con Poisson.

        El parametro de dispersion en [-0.25, 0.25] controla la sobredispersion:
        Valor 0: equivalente a Poisson.
        Valor positivo: mayor sobredispersion (mas partidos de 0-0 y pocos partidos de muchos goles).


        9. PROBABILIDADES Y ODDS JUSTAS

        La vista Probabilidades muestra la chance estimada de cada marcador.

        La vista Odds justas convierte esa probabilidad en cuota decimal sin margen:

            odd justa = 1 / probabilidad

        La vista Odds con margen aplica el Margen % sobre la cuota justa:

            odd con margen = odd justa / (1 + margen / 100)


        10. RESUMEN AGREGADO

        El resumen muestra mercados derivados de la matriz:

            - Victoria local, empate y victoria visitante.
            - Over/Under 0.5, 1.5, 2.5 y 3.5.
            - Both Teams To Score: Si / No.
            - 0-0 y 1-1.
            - Marcador mas probable.

        La matriz visible usa el valor de Max goles. El resumen usa internamente hasta 15 goles para no subestimar mercados agregados cuando la grilla visible es mas chica.


        11. MASA DE MATRIZ Y RESIDUAL

        Masa matriz mostrada: cuanta probabilidad esta cubierta por los marcadores visibles.
        Residual: cuanta probabilidad queda fuera de la grilla visible.


        12. MUNDIAL 2026 (THE ODDS API)

        ScoreMatrix puede importar cuotas del Mundial 2026 desde The Odds API: 1X2, Over/Under y BTTS.

        Flujo:
            1. Ingresa tu API key y la region de bookmakers (eu, uk, us o au).
            2. Pulsa Buscar partidos para cargar los encuentros disponibles.
            3. Selecciona un partido y pulsa Traer cuotas.

        La API key se guarda localmente en %APPDATA%\ScoreMatrix\settings.json.


        13. PENCA: VALOR ESPERADO DE PUNTAJE

        Para cada marcador candidato, la app calcula:

            EV = suma sobre todos los resultados posibles de P(resultado) * puntos(prediccion, resultado)

        Reglas por defecto (Penca Grupo RAS):
            - Resultado exacto: 7 puntos
            - Ganador o empate acertado: 2 puntos
            - Goles de un equipo acertados: 1 punto

        Podes editar los puntos en Pts exacto, Pts resultado y Pts goles.


        14. BACKTEST Y METRICAS DE CALIBRACION

        ScoreMatrix puede evaluar su poder predictivo sobre partidos historicos.

        Flujo:
            1. Prepara un CSV con partidos historicos (ver formato en la documentacion).
            2. Abre la ventana Backtest desde el boton de la pantalla principal.
            3. Carga el CSV, elige el modelo y metodo de de-vig a evaluar y ejecuta el backtest.

        Metricas calculadas:

        RPS (Ranked Probability Score)
        Estandar de la industria para evaluar pronosticos de 3 resultados ordenados.
        Rango: [0, 1]. Menor es mejor.
        Penaliza en forma cuadratica los desvios entre la distribucion acumulada pronosticada y la real.

        Brier Score
        Error cuadratico entre la probabilidad pronosticada y el resultado real (0 o 1) para victoria local.
        Rango: [0, 1]. Menor es mejor.

        Log-Loss
        Penaliza probabilidades extremas que resultan incorrectas. Muy sensible a errores de confianza.

        Curva de calibracion
        Divide los pronosticos en deciles y compara la frecuencia observada con la pronosticada para victoria local.
        Una curva cercana a la diagonal indica buena calibracion.

        Formato del CSV de partidos historicos:
            Columnas requeridas: homeOdds, drawOdds, awayOdds, actualHome, actualAway
            Columnas opcionales: home, away, overOdds, underOdds, ouLine, bttsYes, bttsNo
        """;
}
