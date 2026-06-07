using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BlackBoxIdentification.Core;
using BlackBoxIdentification.IO;
using BlackBoxIdentification.MathModel;
using ScottPlot.WinForms;

namespace BlackBoxIdentification;

public sealed class MainForm : Form
{
    private SignalData? _data;
    private IdentificationResult? _result;

    private readonly DataGridView _grid = new();
    private readonly DataGridView _coeffA = new();
    private readonly DataGridView _coeffC = new();
    private readonly FormsPlot _plotSignals = new();
    private readonly FormsPlot _plotModel = new();
    private readonly FormsPlot _plotResidual = new();
    private readonly FormsPlot _plotKernel1 = new();
    private readonly FormsPlot _plotKernel2Diagonal = new();

    private readonly NumericUpDown _memoryLength = new();
    private readonly NumericUpDown _linearOrder = new();
    private readonly NumericUpDown _quadraticOrder = new();
    private readonly NumericUpDown _collocationNodes = new();
    private readonly ComboBox _method = new();
    private readonly Label _status = new();

    public MainForm()
    {
        Text = "Идентификация параметров динамической системы";
        Width = 1280;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        BuildMenu();
        BuildLayout();
        LoadDemoData();
    }

    private void BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("Файл");
        file.DropDownItems.Add("Открыть Excel...", null, (_, _) => OpenExcel());
        file.DropDownItems.Add("Загрузить демо-данные", null, (_, _) => LoadDemoData());
        file.DropDownItems.Add("Сохранить результаты CSV...", null, (_, _) => SaveResults());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Выход", null, (_, _) => Close());
        var run = new ToolStripMenuItem("Метод");
        run.DropDownItems.Add("Выполнить идентификацию", null, (_, _) => RunIdentification());
        menu.Items.Add(file);
        menu.Items.Add(run);
        MainMenuStrip = menu;
        Controls.Add(menu);
    }

    private void BuildLayout()
    {
        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };

        Controls.Add(root);
        root.BringToFront();

        root.Panel1MinSize = 100;
        root.Panel2MinSize = 100;

        Shown += (_, _) =>
        {
            if (root.Height > 500)
                root.SplitterDistance = 300;
            else
                root.SplitterDistance = root.Height / 2;
        };

        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        root.Panel1.Controls.Add(left);

        var settings = new GroupBox { Text = "Параметры модели", Dock = DockStyle.Top, Height = 235 };
        left.Controls.Add(settings);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        settings.Controls.Add(table);

        _memoryLength.Minimum = 1; _memoryLength.Maximum = 1000; _memoryLength.Value = 50;
        _linearOrder.Minimum = 1; _linearOrder.Maximum = 20; _linearOrder.Value = 3;
        _quadraticOrder.Minimum = 1; _quadraticOrder.Maximum = 20; _quadraticOrder.Value = 3;
        _collocationNodes.Minimum = 3; _collocationNodes.Maximum = 1000; _collocationNodes.Value = 40;
        _method.DropDownStyle = ComboBoxStyle.DropDownList;
        _method.Items.AddRange(Enum.GetNames<IdentificationMethod>());
        _method.SelectedItem = IdentificationMethod.Collocation.ToString();

        AddSettingRow(table, 0, "Метод:", _method);
        AddSettingRow(table, 1, "Длина памяти L, отсчётов:", _memoryLength);
        AddSettingRow(table, 2, "Порядок аппроксимации K1, m1:", _linearOrder);
        AddSettingRow(table, 3, "Порядок аппроксимации K2, m2:", _quadraticOrder);
        AddSettingRow(table, 4, "Количество точек/узлов N:", _collocationNodes);

        var btnRun = new Button { Text = "Рассчитать", Dock = DockStyle.Fill, Height = 34 };
        btnRun.Click += (_, _) => RunIdentification();
        table.Controls.Add(btnRun, 0, 5);
        table.SetColumnSpan(btnRun, 2);

        var info = new GroupBox { Text = "Состояние", Dock = DockStyle.Top, Height = 90 };
        left.Controls.Add(info); info.BringToFront();
        _status.Dock = DockStyle.Fill; _status.Padding = new Padding(8); _status.Text = "Готово.";
        info.Controls.Add(_status);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        root.Panel2.Controls.Add(tabs);
        tabs.TabPages.Add(BuildDataTab());
        tabs.TabPages.Add(BuildPlotsTab());
        tabs.TabPages.Add(BuildCoefficientsTab());
        tabs.TabPages.Add(BuildKernelsTab());
    }

    private static void AddSettingRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private TabPage BuildDataTab()
    {
        var tab = new TabPage("Входные данные");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _plotSignals.Dock = DockStyle.Fill;

        layout.Controls.Add(_grid, 0, 0);
        layout.Controls.Add(_plotSignals, 1, 0);

        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildPlotsTab()
    {
        var tab = new TabPage("Результаты");

        var resultTabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        var compareTab = new TabPage("Сравнение");
        _plotModel.Dock = DockStyle.Fill;
        compareTab.Controls.Add(_plotModel);

        var residualTab = new TabPage("Невязка");
        _plotResidual.Dock = DockStyle.Fill;
        residualTab.Controls.Add(_plotResidual);

        resultTabs.TabPages.Add(compareTab);
        resultTabs.TabPages.Add(residualTab);

        tab.Controls.Add(resultTabs);
        return tab;
    }

    private TabPage BuildCoefficientsTab()
    {
        var tab = new TabPage("Коэффициенты");
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260 };
        _coeffA.Dock = DockStyle.Fill; _coeffA.ReadOnly = true; _coeffA.AllowUserToAddRows = false; _coeffA.AllowUserToDeleteRows = false; _coeffA.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _coeffC.Dock = DockStyle.Fill; _coeffC.ReadOnly = true; _coeffC.AllowUserToAddRows = false; _coeffC.AllowUserToDeleteRows = false; _coeffC.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        split.Panel1.Controls.Add(_coeffA); split.Panel2.Controls.Add(_coeffC); tab.Controls.Add(split); return tab;
    }

    private TabPage BuildKernelsTab()
    {
        var tab = new TabPage("Ядра модели");

        var resultTabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        var kernel1Tab = new TabPage("K1(s)");
        _plotKernel1.Dock = DockStyle.Fill;
        kernel1Tab.Controls.Add(_plotKernel1);

        var kernel2Tab = new TabPage("K2(s, s)");
        _plotKernel2Diagonal.Dock = DockStyle.Fill;
        kernel2Tab.Controls.Add(_plotKernel2Diagonal);

        resultTabs.TabPages.Add(kernel1Tab);
        resultTabs.TabPages.Add(kernel2Tab);

        tab.Controls.Add(resultTabs);
        return tab;
    }

    private void OpenExcel()
    {
        using var dialog = new OpenFileDialog { Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*", Title = "Выберите Excel-файл с колонками x(t), y(t)" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _data = ExcelSignalReader.ReadTwoColumnFile(dialog.FileName);
            _result = null;
            BindData(); PlotInputSignals(); SetStatus($"Загружено точек: {_data.Count}. Файл: {Path.GetFileName(dialog.FileName)}. {GetDataInfo()}");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Ошибка загрузки Excel", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void LoadDemoData()
    {
        _data = DemoDataGenerator.Generate();
        _result = null;
        BindData(); PlotInputSignals(); SetStatus($"Загружены демо-данные: {_data.Count} точек. {GetDataInfo()}");
    }

    private void RunIdentification()
    {
        if (_data is null) { MessageBox.Show(this, "Сначала загрузите данные."); return; }
        try
        {
            var parameters = new IdentificationParameters
            {
                Method = Enum.Parse<IdentificationMethod>(_method.SelectedItem?.ToString() ?? nameof(IdentificationMethod.Collocation)),
                MemoryLength = (int)_memoryLength.Value,
                LinearOrder = (int)_linearOrder.Value,
                QuadraticOrder = (int)_quadraticOrder.Value,
                NodeCount = (int)_collocationNodes.Value
            };
            IIdentifier identifier = parameters.Method == IdentificationMethod.LeastSquares ? new LeastSquaresIdentifier() : new CollocationIdentifier();
            _result = identifier.Identify(_data, parameters);
            BindCoefficients(); PlotResults(); PlotKernels(parameters);

            int parameterCount = DesignMatrixBuilder.GetParameterCount(parameters);
            SetStatus($"Расчёт выполнен. Метод: {parameters.Method}. Неизвестных коэффициентов: {parameterCount}. RMSE = {_result.Rmse:G6}; относительная ошибка = {_result.RelativeErrorPercent:G4}%; max|r| = {_result.MaxAbsoluteError:G6}");
        }
        catch (Exception ex)
        {
            SetStatus($"Расчёт не выполнен: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Ошибка расчёта", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveResults()
    {
        if (_data is null || _result is null) { MessageBox.Show(this, "Нет результатов для сохранения."); return; }
        using var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", Title = "Сохранить результаты", FileName = "identification_results.csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        ResultExporter.SaveCsv(dialog.FileName, _data, _result); SetStatus($"Результаты сохранены: {dialog.FileName}");
    }

    private void BindData()
    {
        if (_data is null) return;
        _grid.DataSource = _data.Points.Select(p => new { t = p.Time, x = p.Input, y = p.Output }).ToList();
    }

    private void BindCoefficients()
    {
        if (_result is null) return;

        var linearRows = new[] { new { Коэффициент = "H0", Значение = _result.ConstantCoefficient } }
            .Concat(_result.LinearCoefficients.Select((v, i) => new { Коэффициент = $"A[{i}]", Значение = v }))
            .ToList();

        _coeffA.DataSource = linearRows;

        _coeffC.DataSource = Enumerable.Range(0, _result.QuadraticCoefficients.GetLength(0))
            .SelectMany(i => Enumerable.Range(0, _result.QuadraticCoefficients.GetLength(1)).Select(j => new { i, j, C = _result.QuadraticCoefficients[i, j] })).ToList();
    }

    private void PlotInputSignals()
    {
        if (_data is null) return;
        var t = _data.Time.ToArray();
        _plotSignals.Plot.Clear();
        _plotSignals.Plot.Add.Scatter(t, _data.Input.ToArray()).LegendText = "x(t), вход";
        _plotSignals.Plot.Add.Scatter(t, _data.Output.ToArray()).LegendText = "y(t), выход";
        _plotSignals.Plot.Title("Входной и выходной сигнал"); _plotSignals.Plot.XLabel("t"); _plotSignals.Plot.Legend.IsVisible = true; _plotSignals.Plot.Axes.AutoScale(); _plotSignals.Refresh();
    }

    private void PlotResults()
    {
        if (_data is null || _result is null) return;
        var t = _data.Time.ToArray();
        _plotModel.Plot.Clear();
        _plotModel.Plot.Add.Scatter(t, _data.Output.ToArray()).LegendText = "y(t)";
        _plotModel.Plot.Add.Scatter(t, _result.ModelOutput).LegendText = "ŷ(t)";
        _plotModel.Plot.Title("Сравнение исходного и восстановленного выхода"); _plotModel.Plot.XLabel("t"); _plotModel.Plot.Legend.IsVisible = true; _plotModel.Plot.Axes.AutoScale(); _plotModel.Refresh();
        _plotResidual.Plot.Clear(); _plotResidual.Plot.Add.Scatter(t, _result.Residual);
        _plotResidual.Plot.Title($"Невязка r(t) = y(t) - ŷ(t), RMSE = {_result.Rmse:G6}, δ = {_result.RelativeErrorPercent:G4}%"); _plotResidual.Plot.XLabel("t"); _plotResidual.Plot.Axes.AutoScale(); _plotResidual.Refresh();
    }

    private void PlotKernels(IdentificationParameters parameters)
    {
        if (_result is null || _data is null) return;

        double[] time = _data.Time.ToArray();
        double step = DesignMatrixBuilder.EstimateStep(time);
        double memoryInterval = Math.Max(step, parameters.MemoryLength * step);
        int pointCount = Math.Max(50, parameters.MemoryLength + 1);

        var sValues = Enumerable.Range(0, pointCount)
            .Select(i => memoryInterval * i / (double)Math.Max(1, pointCount - 1))
            .ToArray();

        var k1Values = sValues.Select(s => EvaluateK1(parameters, s, memoryInterval)).ToArray();
        var k2DiagonalValues = sValues.Select(s => EvaluateK2(parameters, s, s, memoryInterval)).ToArray();

        _plotKernel1.Plot.Clear();
        _plotKernel1.Plot.Add.Scatter(sValues, k1Values);
        _plotKernel1.Plot.Title("Идентифицированное ядро первого порядка K1(s)");
        _plotKernel1.Plot.XLabel("s");
        _plotKernel1.Plot.YLabel("K1(s)");
        _plotKernel1.Plot.Axes.AutoScale();
        _plotKernel1.Refresh();

        _plotKernel2Diagonal.Plot.Clear();
        _plotKernel2Diagonal.Plot.Add.Scatter(sValues, k2DiagonalValues);
        _plotKernel2Diagonal.Plot.Title("Сечение ядра второго порядка K2(s, s)");
        _plotKernel2Diagonal.Plot.XLabel("s");
        _plotKernel2Diagonal.Plot.YLabel("K2(s, s)");
        _plotKernel2Diagonal.Plot.Axes.AutoScale();
        _plotKernel2Diagonal.Refresh();
    }

    private double EvaluateK1(IdentificationParameters parameters, double s, double memoryInterval)
    {
        if (_result is null) return 0.0;

        double z = ChebyshevBasis.MapToMinusOneOne(s, memoryInterval);
        double value = 0.0;

        for (int i = 0; i < parameters.LinearOrder; i++)
            value += _result.LinearCoefficients[i] * ChebyshevBasis.T(i, z);

        return value;
    }

    private double EvaluateK2(IdentificationParameters parameters, double s1, double s2, double memoryInterval)
    {
        if (_result is null) return 0.0;

        double z1 = ChebyshevBasis.MapToMinusOneOne(s1, memoryInterval);
        double z2 = ChebyshevBasis.MapToMinusOneOne(s2, memoryInterval);
        double value = 0.0;

        for (int i = 0; i < parameters.QuadraticOrder; i++)
        {
            double ti = ChebyshevBasis.T(i, z1);

            for (int j = 0; j < parameters.QuadraticOrder; j++)
            {
                double tj = ChebyshevBasis.T(j, z2);
                value += _result.QuadraticCoefficients[i, j] * ti * tj;
            }
        }

        return value;
    }

    private string GetDataInfo()
    {
        if (_data is null || _data.Count < 2)
            return string.Empty;

        double t0 = _data.Points[0].Time;
        double t1 = _data.Points[_data.Count - 1].Time;
        double h = _data.Points[1].Time - _data.Points[0].Time;

        return $"Интервал: [{t0:G4}; {t1:G4}], шаг h ≈ {h:G4}.";
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        // 
        // MainForm
        // 
        ClientSize = new Size(282, 253);
        Name = "MainForm";
        Load += MainForm_Load;
        ResumeLayout(false);
    }

    private void SetStatus(string message) => _status.Text = message;

    private void MainForm_Load(object sender, EventArgs e)
    {

    }
}
