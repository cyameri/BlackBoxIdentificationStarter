using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BlackBoxIdentification.Controls;
using BlackBoxIdentification.Core;
using BlackBoxIdentification.IO;
using BlackBoxIdentification.MathModel;
using ScottPlot.WinForms;

namespace BlackBoxIdentification;

public sealed class MainForm : Form
{
    private SignalData? _data;
    private IdentificationResult? _result;
    private IdentificationParameters? _lastParameters;

    private readonly DataGridView _grid = new();
    private readonly DataGridView _coeffA = new();
    private readonly DataGridView _coeffC = new();

    private readonly FormsPlot _plotSignals = new();
    private readonly FormsPlot _plotModel = new();
    private readonly FormsPlot _plotResidual = new();
    private readonly FormsPlot _plotKernel1 = new();
    private readonly FormsPlot _plotKernel2Diagonal = new();
    private readonly SurfacePlot3DControl _plotKernel2Surface = new();

    private readonly NumericUpDown _basisK1 = new();
    private readonly NumericUpDown _basisK2S1 = new();
    private readonly NumericUpDown _basisK2S2 = new();
    private readonly NumericUpDown _pointCount = new();
    private readonly ComboBox _method = new();
    private readonly Label _pointCountLabel = new();
    private readonly Label _status = new();
    private IdentificationMethod _lastUiMethod = IdentificationMethod.Collocation;

    public MainForm()
    {
        Text = "Идентификация параметров динамической системы";
        Width = 1320;
        Height = 860;
        MinimumSize = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterScreen;

        BuildMenu();
        BuildLayout();
        LoadDemoData();
        UpdateMethodControls();
    }

    private void BuildMenu()
    {
        var menu = new MenuStrip();

        var file = new ToolStripMenuItem("Файл");
        file.DropDownItems.Add("Открыть Excel...", null, (_, _) => OpenExcel());
        file.DropDownItems.Add("Загрузить демонстрационные данные", null, (_, _) => LoadDemoData());
        file.DropDownItems.Add("Сохранить результаты CSV...", null, (_, _) => SaveResults());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Выход", null, (_, _) => Close());

        menu.Items.Add(file);
        MainMenuStrip = menu;
        Controls.Add(menu);
    }

    private void BuildLayout()
    {
        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.Panel1
        };

        Controls.Add(root);
        root.BringToFront();
        root.Panel1MinSize = 290;
        root.Panel2MinSize = 200;

        Shown += (_, _) =>
        {
            if (root.Height > 650)
                root.SplitterDistance = 320;
        };

        // Верхняя часть построена через TableLayoutPanel, чтобы блок состояния
        // никогда не перекрывал кнопку расчёта при изменении размера окна.
        var upperLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        upperLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        upperLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 232.0f));
        upperLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
        root.Panel1.Controls.Add(upperLayout);

        var settings = new GroupBox
        {
            Text = "Параметры модели",
            Dock = DockStyle.Fill
        };
        upperLayout.Controls.Add(settings, 0, 0);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(8)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57.0f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43.0f));

        for (int row = 0; row < 5; row++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30.0f));

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38.0f));
        settings.Controls.Add(table);

        ConfigureNumeric(_basisK1, 1, 12, 3);
        ConfigureNumeric(_basisK2S1, 1, 12, 3);
        ConfigureNumeric(_basisK2S2, 1, 12, 3);
        ConfigureNumeric(_pointCount, 2, 5000, 40);

        _method.DropDownStyle = ComboBoxStyle.DropDownList;
        _method.Items.AddRange(new object[]
        {
            "Метод коллокации",
            "Метод наименьших квадратов"
        });
        _method.SelectedIndex = 0;
        _method.SelectedIndexChanged += (_, _) => UpdateMethodControls();

        _basisK1.ValueChanged += (_, _) => UpdateMethodControls();
        _basisK2S1.ValueChanged += (_, _) => UpdateMethodControls();
        _basisK2S2.ValueChanged += (_, _) => UpdateMethodControls();

        AddSettingRow(table, 0, "Метод:", _method);
        AddSettingRow(table, 1, "Базисных функций первого ядра K1, m:", _basisK1);
        AddSettingRow(table, 2, "Базисных функций K2 по переменной s1, m1:", _basisK2S1);
        AddSettingRow(table, 3, "Базисных функций K2 по переменной s2, m2:", _basisK2S2);

        _pointCountLabel.Dock = DockStyle.Fill;
        _pointCountLabel.TextAlign = ContentAlignment.MiddleLeft;
        table.Controls.Add(_pointCountLabel, 0, 4);
        _pointCount.Dock = DockStyle.Fill;
        table.Controls.Add(_pointCount, 1, 4);

        var runButton = new Button
        {
            Text = "Рассчитать",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 3)
        };
        runButton.Click += (_, _) => RunIdentification();
        table.Controls.Add(runButton, 0, 5);
        table.SetColumnSpan(runButton, 2);

        var tooltip = new ToolTip();
        tooltip.SetToolTip(
            _pointCount,
            "Для коллокации N вычисляется автоматически: N = m + m1*m2. " +
            "Для МНК задается число используемых точек.");

        var statusGroup = new GroupBox
        {
            Text = "Состояние",
            Dock = DockStyle.Fill
        };
        upperLayout.Controls.Add(statusGroup, 0, 1);

        _status.Dock = DockStyle.Fill;
        _status.Padding = new Padding(8);
        _status.Text = "Готово.";
        _status.AutoEllipsis = true;
        statusGroup.Controls.Add(_status);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        root.Panel2.Controls.Add(tabs);
        tabs.TabPages.Add(BuildDataTab());
        tabs.TabPages.Add(BuildPlotsTab());
        tabs.TabPages.Add(BuildCoefficientsTab());
        tabs.TabPages.Add(BuildKernelsTab());
    }

    private static void ConfigureNumeric(NumericUpDown control, int minimum, int maximum, int value)
    {
        control.Minimum = minimum;
        control.Maximum = maximum;
        control.Value = value;
    }

    private static void AddSettingRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);

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

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54.0f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46.0f));

        ConfigureGrid(_grid);
        _plotSignals.Dock = DockStyle.Fill;

        layout.Controls.Add(_grid, 0, 0);
        layout.Controls.Add(_plotSignals, 1, 0);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildPlotsTab()
    {
        var tab = new TabPage("Результаты");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(4, 4, 8, 2)
        };

        var resetScaleButton = new Button
        {
            Text = "Сбросить масштаб",
            AutoSize = true,
            Margin = new Padding(3, 3, 3, 3)
        };
        resetScaleButton.Click += (_, _) => ResetResultPlotScales();
        toolbar.Controls.Add(resetScaleButton);

        var hint = new Label
        {
            Text = "Исходный выход показан сплошной линией, восстановленный — пунктиром с маркерами.",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 5, 0, 0)
        };
        toolbar.Controls.Add(hint);

        var resultTabs = new TabControl { Dock = DockStyle.Fill };

        var compareTab = new TabPage("Сравнение");
        _plotModel.Dock = DockStyle.Fill;
        _plotModel.DoubleClick += (_, _) => ResetResultPlotScales();
        compareTab.Controls.Add(_plotModel);

        var residualTab = new TabPage("Невязка");
        _plotResidual.Dock = DockStyle.Fill;
        _plotResidual.DoubleClick += (_, _) => ResetResultPlotScales();
        residualTab.Controls.Add(_plotResidual);

        resultTabs.TabPages.Add(compareTab);
        resultTabs.TabPages.Add(residualTab);

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(resultTabs, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildCoefficientsTab()
    {
        var tab = new TabPage("Коэффициенты");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45.0f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55.0f));

        var linearGroup = new GroupBox
        {
            Text = "Коэффициенты первого ядра K1(s)",
            Dock = DockStyle.Fill
        };
        var linearLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6)
        };
        linearLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30.0f));
        linearLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
        linearLayout.Controls.Add(new Label
        {
            Text = "K1(s) = Σ AᵢTᵢ(s). В таблице указаны базисная функция, обозначение и значение Aᵢ.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        ConfigureGrid(_coeffA);
        linearLayout.Controls.Add(_coeffA, 0, 1);
        linearGroup.Controls.Add(linearLayout);

        var quadraticGroup = new GroupBox
        {
            Text = "Матрица коэффициентов второго ядра K2(s1, s2)",
            Dock = DockStyle.Fill
        };
        var quadraticLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6)
        };
        quadraticLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34.0f));
        quadraticLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
        quadraticLayout.Controls.Add(new Label
        {
            Text = "K2(s1, s2) = ΣΣ CᵢⱼTᵢ(s1)Tⱼ(s2). Строки соответствуют i, столбцы — j.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        ConfigureGrid(_coeffC);
        quadraticLayout.Controls.Add(_coeffC, 0, 1);
        quadraticGroup.Controls.Add(quadraticLayout);

        layout.Controls.Add(linearGroup, 0, 0);
        layout.Controls.Add(quadraticGroup, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildKernelsTab()
    {
        var tab = new TabPage("Ядра модели");
        var kernelTabs = new TabControl { Dock = DockStyle.Fill };

        var kernel1Tab = new TabPage("K1(s)");
        _plotKernel1.Dock = DockStyle.Fill;
        kernel1Tab.Controls.Add(_plotKernel1);

        var kernel2SurfaceTab = new TabPage("K2(s1, s2), 3D");
        _plotKernel2Surface.Dock = DockStyle.Fill;
        kernel2SurfaceTab.Controls.Add(_plotKernel2Surface);

        var kernel2DiagonalTab = new TabPage("Диагональ K2(s, s)");
        _plotKernel2Diagonal.Dock = DockStyle.Fill;
        kernel2DiagonalTab.Controls.Add(_plotKernel2Diagonal);

        kernelTabs.TabPages.Add(kernel1Tab);
        kernelTabs.TabPages.Add(kernel2SurfaceTab);
        kernelTabs.TabPages.Add(kernel2DiagonalTab);
        tab.Controls.Add(kernelTabs);
        return tab;
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.RowTemplate.Height = 26;
        grid.RowHeadersVisible = false;
        grid.BackgroundColor = SystemColors.Window;
        grid.BorderStyle = BorderStyle.Fixed3D;
    }

    private void UpdateMethodControls()
    {
        if (_method.SelectedItem is null)
            return;

        IdentificationMethod method = SelectedMethod();
        bool methodChanged = method != _lastUiMethod;
        int parameterCount = CurrentParameterCount();

        if (method == IdentificationMethod.Collocation)
        {
            _pointCountLabel.Text = "Узлов коллокации N = m + m1*m2:";
            _pointCount.Enabled = false;
            _pointCount.Minimum = 1;
            _pointCount.Value = Math.Min((decimal)parameterCount, _pointCount.Maximum);
        }
        else
        {
            _pointCountLabel.Text = "Количество точек метода наименьших квадратов N:";
            _pointCount.Enabled = true;

            decimal minimum = Math.Min(_pointCount.Maximum, parameterCount + 1);
            _pointCount.Minimum = minimum;

            if (methodChanged && _data is not null)
            {
                _pointCount.Value = Math.Min((decimal)_data.Count, _pointCount.Maximum);
            }
            else if (_pointCount.Value < minimum)
            {
                _pointCount.Value = minimum;
            }
        }

        _lastUiMethod = method;
    }

    private int CurrentParameterCount()
    {
        return (int)_basisK1.Value + (int)_basisK2S1.Value * (int)_basisK2S2.Value;
    }

    private IdentificationMethod SelectedMethod()
    {
        return _method.SelectedIndex == 1
            ? IdentificationMethod.LeastSquares
            : IdentificationMethod.Collocation;
    }

    private static string MethodDisplayName(IdentificationMethod method)
    {
        return method == IdentificationMethod.Collocation
            ? "метод коллокации"
            : "метод наименьших квадратов";
    }

    private void UseAllAvailablePointsForLeastSquares()
    {
        if (_data is null || SelectedMethod() != IdentificationMethod.LeastSquares)
            return;

        decimal minimum = Math.Min(_pointCount.Maximum, CurrentParameterCount() + 1);
        _pointCount.Minimum = minimum;
        _pointCount.Value = Math.Min((decimal)_data.Count, _pointCount.Maximum);
    }

    private IdentificationParameters ReadParameters()
    {
        IdentificationMethod method = SelectedMethod();

        return new IdentificationParameters
        {
            Method = method,
            FirstKernelBasisCount = (int)_basisK1.Value,
            SecondKernelBasisCountS1 = (int)_basisK2S1.Value,
            SecondKernelBasisCountS2 = (int)_basisK2S2.Value,
            LeastSquaresPointCount = (int)_pointCount.Value,
            NormalizeSignals = false,
            QuadratureIntervals = 160
        };
    }

    private void OpenExcel()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            Title = "Выберите Excel-файл с колонками x,y или t,x,y"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            SignalData loaded = ExcelSignalReader.ReadTwoColumnFile(dialog.FileName);
            _data = SignalPreprocessor.NormalizeTimeToUnitInterval(loaded);
            ResetResults();
            UseAllAvailablePointsForLeastSquares();
            BindData();
            PlotInputSignals();
            SetStatus(
                $"Загружено точек: {_data.Count}. Файл: {Path.GetFileName(dialog.FileName)}. " +
                "Временная ось приведена к интервалу [0; 1]. Амплитуды x и y не изменены." +
                BuildInitialValueWarning(_data));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Ошибка загрузки Excel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void LoadDemoData()
    {
        _data = DemoDataGenerator.Generate();
        ResetResults();
        UseAllAvailablePointsForLeastSquares();
        BindData();
        PlotInputSignals();
        SetStatus(
            $"Загружен демонстрационный набор: {_data.Count} точек, t ∈ [0; 1]. " +
            "Для проверки используются заранее заданные входной и выходной сигналы.");
    }

    private void RunIdentification()
    {
        if (_data is null)
        {
            MessageBox.Show(this, "Сначала загрузите данные.");
            return;
        }

        try
        {
            IdentificationParameters parameters = ReadParameters();
            IIdentifier identifier = parameters.Method == IdentificationMethod.LeastSquares
                ? new LeastSquaresIdentifier()
                : new CollocationIdentifier();

            UseWaitCursor = true;
            _status.Text = "Выполняется расчет...";
            Refresh();

            _result = identifier.Identify(_data, parameters);
            _lastParameters = parameters;

            BindCoefficients();
            PlotResults();
            PlotKernels(parameters);

            SetStatus(
                $"Расчет выполнен: {MethodDisplayName(parameters.Method)}. " +
                $"Неизвестных коэффициентов: {_result.ParameterCount}; уравнений: {_result.EquationCount}. " +
                $"RMSE = {_result.Rmse:G6}; относительная ошибка = {_result.RelativeErrorPercent:G4}%; " +
                $"max|r| = {_result.MaxAbsoluteError:G6}.");
        }
        catch (Exception exception)
        {
            SetStatus($"Расчет не выполнен: {exception.Message}");
            MessageBox.Show(
                this,
                exception.Message,
                "Ошибка расчета",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void SaveResults()
    {
        if (_data is null || _result is null)
        {
            MessageBox.Show(this, "Нет результатов для сохранения.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            Title = "Сохранить результаты",
            FileName = "identification_results.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        ResultExporter.SaveCsv(dialog.FileName, _data, _result);
        SetStatus($"Результаты сохранены: {dialog.FileName}");
    }

    private void ResetResults()
    {
        _result = null;
        _lastParameters = null;
        ClearCoefficientGrids();

        _plotModel.Plot.Clear();
        _plotModel.Refresh();
        _plotResidual.Plot.Clear();
        _plotResidual.Refresh();
        _plotKernel1.Plot.Clear();
        _plotKernel1.Refresh();
        _plotKernel2Diagonal.Plot.Clear();
        _plotKernel2Diagonal.Refresh();
        _plotKernel2Surface.ClearSurface();
    }

    private void BindData()
    {
        if (_data is null)
            return;

        _grid.DataSource = _data.Points
            .Select(point => new
            {
                t = point.Time,
                x = point.Input,
                y = point.Output
            })
            .ToList();
    }

    private void ClearCoefficientGrids()
    {
        _coeffA.DataSource = null;
        _coeffA.Columns.Clear();
        _coeffA.Rows.Clear();

        _coeffC.DataSource = null;
        _coeffC.Columns.Clear();
        _coeffC.Rows.Clear();
    }

    private void BindCoefficients()
    {
        if (_result is null)
            return;

        ClearCoefficientGrids();

        _coeffA.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Базисная функция",
            FillWeight = 34.0f,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _coeffA.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Коэффициент",
            FillWeight = 26.0f,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _coeffA.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Значение",
            FillWeight = 40.0f,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "G12" }
        });

        for (int i = 0; i < _result.LinearCoefficients.Length; i++)
        {
            _coeffA.Rows.Add(
                $"T_{i}(s)",
                $"A_{i}",
                _result.LinearCoefficients[i]);
        }

        int rows = _result.QuadraticCoefficients.GetLength(0);
        int columns = _result.QuadraticCoefficients.GetLength(1);

        _coeffC.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "i / j",
            FillWeight = 28.0f,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter
            }
        });

        for (int j = 0; j < columns; j++)
        {
            _coeffC.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = $"j = {j}",
                FillWeight = 100.0f,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "G10",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
        }

        for (int i = 0; i < rows; i++)
        {
            var values = new object[columns + 1];
            values[0] = $"i = {i}";

            for (int j = 0; j < columns; j++)
                values[j + 1] = _result.QuadraticCoefficients[i, j];

            _coeffC.Rows.Add(values);
        }

        _coeffA.ClearSelection();
        _coeffC.ClearSelection();
    }

    private void PlotInputSignals()
    {
        if (_data is null)
            return;

        double[] time = _data.Time.ToArray();

        _plotSignals.Plot.Clear();

        var inputPlot = _plotSignals.Plot.Add.Scatter(time, _data.Input.ToArray());
        inputPlot.LegendText = "x(t), вход";
        inputPlot.MarkerSize = 0;
        inputPlot.LineWidth = 2.0f;

        var outputPlot = _plotSignals.Plot.Add.Scatter(time, _data.Output.ToArray());
        outputPlot.LegendText = "y(t), выход";
        outputPlot.MarkerSize = 0;
        outputPlot.LineWidth = 2.0f;

        _plotSignals.Plot.Title("Входной и выходной сигналы, t в [0; 1]");
        _plotSignals.Plot.XLabel("t");
        _plotSignals.Plot.Legend.IsVisible = true;
        _plotSignals.Plot.Axes.AutoScale();
        _plotSignals.Refresh();
    }

    private void PlotResults()
    {
        if (_data is null || _result is null)
            return;

        double[] time = _data.Time.ToArray();

        _plotModel.Plot.Clear();

        var actualPlot = _plotModel.Plot.Add.Scatter(time, _data.Output.ToArray());
        actualPlot.LegendText = "y(t), исходный";
        actualPlot.MarkerSize = 0;
        actualPlot.LineWidth = 2.5f;

        var modelPlot = _plotModel.Plot.Add.Scatter(time, _result.ModelOutput);
        modelPlot.LegendText = "ŷ(t), восстановленный";
        modelPlot.MarkerSize = 4;
        modelPlot.LineWidth = 1.5f;
        modelPlot.LinePattern = ScottPlot.LinePattern.Dashed;

        _plotModel.Plot.Title(
            $"Сравнение исходного и восстановленного выхода, RMSE = {_result.Rmse:G6}");
        _plotModel.Plot.XLabel("t");
        _plotModel.Plot.Legend.IsVisible = true;
        _plotModel.Plot.Axes.AutoScale();
        _plotModel.Refresh();

        _plotResidual.Plot.Clear();
        var residualPlot = _plotResidual.Plot.Add.Scatter(time, _result.Residual);
        residualPlot.MarkerSize = 3;
        residualPlot.LineWidth = 1.5f;
        _plotResidual.Plot.Title(
            $"Невязка r(t)=y(t)-ŷ(t), RMSE={_result.Rmse:G6}, δ={_result.RelativeErrorPercent:G4}%");
        _plotResidual.Plot.XLabel("t");
        _plotResidual.Plot.Axes.AutoScale();
        _plotResidual.Refresh();
    }

    private void ResetResultPlotScales()
    {
        _plotModel.Plot.Axes.AutoScale();
        _plotResidual.Plot.Axes.AutoScale();
        _plotModel.Refresh();
        _plotResidual.Refresh();
    }

    private void PlotKernels(IdentificationParameters parameters)
    {
        if (_result is null)
            return;

        const int linePointCount = 121;
        double[] sValues = Enumerable
            .Range(0, linePointCount)
            .Select(index => index / (double)(linePointCount - 1))
            .ToArray();

        double[] kernel1 = sValues
            .Select(s => EvaluateK1(parameters, s))
            .ToArray();

        double[] diagonal = sValues
            .Select(s => EvaluateK2(parameters, s, s))
            .ToArray();

        _plotKernel1.Plot.Clear();
        _plotKernel1.Plot.Add.Scatter(sValues, kernel1);
        _plotKernel1.Plot.Title("Идентифицированное ядро первого порядка K1(s)");
        _plotKernel1.Plot.XLabel("s");
        _plotKernel1.Plot.YLabel("K1(s)");
        _plotKernel1.Plot.Axes.AutoScale();
        _plotKernel1.Refresh();

        const int surfaceSize = 41;
        var surface = new double[surfaceSize, surfaceSize];

        for (int i = 0; i < surfaceSize; i++)
        {
            double s1 = i / (double)(surfaceSize - 1);

            for (int j = 0; j < surfaceSize; j++)
            {
                double s2 = j / (double)(surfaceSize - 1);
                surface[i, j] = EvaluateK2(parameters, s1, s2);
            }
        }

        _plotKernel2Surface.Title = "Идентифицированная поверхность K2(s1, s2), s1, s2 ∈ [0; 1]";
        _plotKernel2Surface.SetSurface(surface);

        _plotKernel2Diagonal.Plot.Clear();
        _plotKernel2Diagonal.Plot.Add.Scatter(sValues, diagonal);
        _plotKernel2Diagonal.Plot.Title("Дополнительное диагональное сечение K2(s, s)");
        _plotKernel2Diagonal.Plot.XLabel("s");
        _plotKernel2Diagonal.Plot.YLabel("K2(s, s)");
        _plotKernel2Diagonal.Plot.Axes.AutoScale();
        _plotKernel2Diagonal.Refresh();
    }

    private double EvaluateK1(IdentificationParameters parameters, double s)
    {
        if (_result is null)
            return 0.0;

        double value = 0.0;
        for (int i = 0; i < parameters.FirstKernelBasisCount; i++)
            value += _result.LinearCoefficients[i] * ChebyshevBasis.T(i, s);

        return value;
    }

    private double EvaluateK2(IdentificationParameters parameters, double s1, double s2)
    {
        if (_result is null)
            return 0.0;

        double value = 0.0;

        for (int i = 0; i < parameters.SecondKernelBasisCountS1; i++)
        {
            double firstBasis = ChebyshevBasis.T(i, s1);

            for (int j = 0; j < parameters.SecondKernelBasisCountS2; j++)
            {
                double secondBasis = ChebyshevBasis.T(j, s2);
                value += _result.QuadraticCoefficients[i, j] * firstBasis * secondBasis;
            }
        }

        return value;
    }

    private static string BuildInitialValueWarning(SignalData data)
    {
        if (data.Count == 0)
            return string.Empty;

        double maximumOutput = data.Points.Max(point => Math.Abs(point.Output));
        double initialOutput = data.Points[0].Output;
        double threshold = Math.Max(1e-8, maximumOutput * 0.05);

        if (Math.Abs(initialOutput) <= threshold)
            return string.Empty;

        return $" Внимание: y(0) = {initialOutput:G6}. Модель без свободного члена всегда даёт ŷ(0) = 0, " +
               "поэтому для данных с большой постоянной составляющей ошибка в начале неизбежна.";
    }

    private void SetStatus(string message)
    {
        _status.Text = message;
    }
}
