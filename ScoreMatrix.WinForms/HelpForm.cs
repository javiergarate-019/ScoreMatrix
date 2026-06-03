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

        Usar O/U 2.5
        En modo Odds 1X2 podes activar tambien cuotas Over 2.5 y Under 2.5. Esto ayuda a calibrar mejor el total esperado de goles.

        Usar BTTS
        En modo Odds 1X2 podes activar cuotas Both Teams To Score Si/No. Esto ayuda a calibrar la probabilidad de que ambos equipos conviertan.

        Goles esperados
        Ingresas directamente Lambda local y Lambda visitante. En este modo no se usan las odds 1X2 ni se optimizan lambdas.


        2. DE ODDS A PROBABILIDADES SIN MARGEN

        La app convierte cada odd en probabilidad implicita:

            probabilidad bruta = 1 / odd

        Luego normaliza las tres probabilidades para que sumen 100%. Esto quita el margen de forma proporcional:

            p local = p local bruta / suma
            p empate = p empate bruta / suma
            p visitante = p visitante bruta / suma

        Es un metodo simple y transparente. No intenta adivinar como distribuyo el margen la casa de apuestas.


        3. DE PROBABILIDADES 1X2 A LAMBDAS

        Lambda local y Lambda visitante representan los goles esperados de cada equipo.

        Para obtenerlos desde odds 1X2, la app busca dos lambdas que produzcan probabilidades parecidas a las del mercado:

            - Victoria local: suma de marcadores donde local > visitante.
            - Empate: suma de marcadores donde local = visitante.
            - Victoria visitante: suma de marcadores donde local < visitante.

        El optimizador minimiza este error:

            error =
              (local modelo - local mercado)^2
            + (empate modelo - empate mercado)^2
            + (visitante modelo - visitante mercado)^2

        Limitacion importante:
        Las odds 1X2 no dicen directamente cuantos goles espera el mercado. Dos partidos distintos pueden tener probabilidades 1X2 parecidas pero totales de goles diferentes. Para anclar mejor el total harian falta odds adicionales como Over/Under 2.5 o BTTS.

        Si activas Usar O/U 2.5, la app agrega Over 2.5 y Under 2.5 al objetivo del optimizador. El resultado es un compromiso entre encajar el 1X2 y encajar el total de goles.

        Si activas Usar BTTS, la app tambien agrega BTTS Si y BTTS No al objetivo. El resultado busca encajar mejor la chance de que ambos equipos marquen.


        4. MODELO POISSON

        El modelo base asume:

            goles local ~ Poisson(lambda local)
            goles visitante ~ Poisson(lambda visitante)

        La probabilidad de un marcador exacto se calcula asi:

            P(i-j) = Poisson(i, lambda local) * Poisson(j, lambda visitante)

        Ejemplo:
        P(2-1) = probabilidad de que el local haga 2 goles multiplicada por la probabilidad de que el visitante haga 1 gol.


        5. MODELO DIXON-COLES

        Dixon-Coles parte de Poisson, pero ajusta marcadores bajos:

            - 0-0
            - 1-0
            - 0-1
            - 1-1

        El parametro rho controla el ajuste.

        rho = 0
        Queda igual que Poisson simple.

        rho < 0
        Segun el factor Dixon-Coles aplicado, sube 0-0 y 1-1, y baja 1-0 y 0-1.

        rho > 0
        Segun el factor Dixon-Coles aplicado, baja 0-0 y 1-1, y sube 1-0 y 0-1.

        Despues del ajuste, ScoreMatrix renormaliza la matriz para conservar la masa de probabilidad base. Por eso el efecto visible puede variar levemente en la grilla completa.

        Dixon-Coles no agrega informacion nueva del mercado. Solo cambia la forma de distribuir probabilidad entre marcadores bajos.


        6. PROBABILIDADES Y ODDS JUSTAS

        La vista Probabilidades muestra la chance estimada de cada marcador.

        La vista Odds justas convierte esa probabilidad en cuota decimal sin margen:

            odd justa = 1 / probabilidad

        Ejemplo:
        Si un marcador tiene 12.5% de probabilidad:

            odd justa = 1 / 0.125 = 8.00

        La vista Odds con margen aplica el Margen % indicado sobre la cuota justa:

            odd con margen = odd justa / (1 + margen / 100)

        Ejemplo:
        Si la odd justa es 8.00 y el margen es 5%:

            odd con margen = 8.00 / 1.05 = 7.62


        7. RESUMEN AGREGADO

        El resumen muestra mercados derivados de la matriz:

            - Victoria local, empate y victoria visitante.
            - Over/Under 0.5, 1.5, 2.5 y 3.5.
            - Both Teams To Score: Si / No.
            - 0-0 y 1-1.
            - Marcador mas probable.

        La matriz visible usa el valor de Max goles. El resumen usa internamente hasta 15 goles para no subestimar mercados agregados cuando la grilla visible es mas chica.


        8. MASA DE MATRIZ Y RESIDUAL

        Masa matriz mostrada
        Indica cuanta probabilidad esta cubierta por los marcadores visibles.

        Residual fuera de rango
        Indica cuanta probabilidad queda fuera de la matriz visible.

        Ejemplo:
        Si Max goles es 6, resultados como 7-0 o 6-2 no aparecen en la grilla visible. Esa probabilidad forma parte del residual.


        9. INTERPRETACION

        ScoreMatrix no predice resultados garantizados. Calcula probabilidades y cuotas justas segun el modelo elegido.

        Las cuotas reales de bookmakers incluyen margen, liquidez, sesgos de mercado y ajustes comerciales. Por eso las odds justas del modelo pueden diferir de las cuotas reales.
        """;
}
