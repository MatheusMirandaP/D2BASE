using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Globalization;
using System.Threading;
using System.Windows.Markup;
using System.Text.Json;
using System.IO;
using Microsoft.Win32;

namespace CirclePointDistributor
{
    public partial class MainWindow : Window
    {
        // Data Model for Grid
        public class ResultPoint
        {
            public int Id { get; set; }
            public double RealX { get; set; }
            public double RealY { get; set; }
            public double Nmax { get; set; }
            public double Nmin { get; set; }
            public double H { get; set; }

            // Computed for Canvas (not displayed in grid usually, but useful to have)
            public double CanvasX { get; set; }
            public double CanvasY { get; set; }

            public FontWeight NmaxFontWeight { get; set; } = FontWeights.Normal;
            public FontWeight NminFontWeight { get; set; } = FontWeights.Normal;
        }

        // Layer Data Model
        public class LayerData
        {
            public string Points { get; set; } = "4";
            public string Diameter { get; set; } = "2";
        }

        private List<ResultPoint> _calculatedPoints = new List<ResultPoint>();
        
        // Wind calculation results
        private double _calculatedFa = 0;
        private double _calculatedM = 0;

        // File Management
        private string? _currentFilePath = null;

        public class ProjectData
        {
            public string LayersCount { get; set; } = "1";
            public List<LayerData> Layers { get; set; } = new List<LayerData>();
            
            // For backward compatibility
            public string? Points { get; set; }
            public string? Diameter { get; set; }
            
            public string WindH { get; set; }
            public string WindS1 { get; set; }
            public string WindS2 { get; set; }
            public string WindS3 { get; set; }
            public string WindV0 { get; set; }
            public string WindCa { get; set; }
            public string PesoReservatorio { get; set; }
            public string VolumeReservatorio { get; set; }
            public string PesoBloco { get; set; }
            public string MomentoBase { get; set; }
            public string ForcaHorizontal { get; set; }
            public bool ConsiderPb { get; set; }
        }

        public MainWindow(string? initialFilePath = null)
        {
            // Set culture for decimal separators (comma)
            var culture = new CultureInfo("pt-BR");
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            this.Language = XmlLanguage.GetLanguage("pt-BR");

            InitializeComponent();
            UpdateTitle();

            string processedPath = initialFilePath?.Trim('\"');
            if (!string.IsNullOrEmpty(processedPath) && File.Exists(processedPath))
            {
                // We need to wait for the window to load to ensure UI elements are ready
                this.Loaded += (s, e) => LoadFromFile(processedPath);
            }
        }

        // --- File Operations ---

        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Deseja iniciar um novo projeto? Todas as alterações não salvas serão perdidas.", "Novo Projeto", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ResetInputs();
                _currentFilePath = null;
                UpdateTitle();
                
                // Go to welcome view
                ResultsView.Visibility = Visibility.Collapsed;
                ConfigView.Visibility = Visibility.Collapsed;
                ChoiceView.Visibility = Visibility.Collapsed;
                WindInputView.Visibility = Visibility.Collapsed;
                WindResultView.Visibility = Visibility.Collapsed;
                LoadDataView.Visibility = Visibility.Collapsed;
                WelcomeView.Visibility = Visibility.Visible;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Arquivos de Projeto (*.res)|*.res|Todos os arquivos (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadFromFile(openFileDialog.FileName);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveAs_Click(sender, e);
            }
            else
            {
                SaveToFile(_currentFilePath);
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Arquivos de Projeto (*.res)|*.res|Todos os arquivos (*.*)|*.*";
            saveFileDialog.DefaultExt = "res";
            if (saveFileDialog.ShowDialog() == true)
            {
                SaveToFile(saveFileDialog.FileName);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SaveToFile(string path)
        {
            try
            {
                var data = new ProjectData
                {
                    LayersCount = LayersCountInput.Text,
                    Layers = GetLayersFromUI(),
                    
                    WindH = WindHInput.Text,
                    WindS1 = WindS1Input.Text,
                    WindS2 = WindS2Input.Text,
                    WindS3 = WindS3Input.Text,
                    WindV0 = WindV0Input.Text,
                    WindCa = WindCaInput.Text,
                    PesoReservatorio = PesoReservatorioInput.Text,
                    VolumeReservatorio = VolumeReservatorioInput.Text,
                    PesoBloco = PesoBlocoInput.Text,
                    MomentoBase = MomentoBaseInput.Text,
                    ForcaHorizontal = ForcaHorizontalInput.Text,
                    ConsiderPb = ConsiderPbCheckbox.IsChecked == true
                };

                string jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, jsonString);
                
                _currentFilePath = path;
                UpdateTitle();
                MessageBox.Show("Projeto salvo com sucesso!", "Salvar", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFromFile(string path)
        {
            try
            {
                string jsonString = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<ProjectData>(jsonString);

                if (data != null)
                {
                    // Compatibility check
                    if (data.Layers.Count == 0 && !string.IsNullOrEmpty(data.Points))
                    {
                        data.LayersCount = "1";
                        data.Layers.Add(new LayerData { Points = data.Points, Diameter = data.Diameter ?? "2" });
                    }

                    LayersCountInput.Text = data.LayersCount;
                    // Trigger dynamic UI generation
                    UpdateLayersUI();
                    
                    // Fill dynamic layers
                    var currentStacks = LayersContainer.Children.OfType<StackPanel>().ToList();
                    for (int i = 0; i < data.Layers.Count; i++)
                    {
                        if (i < currentStacks.Count)
                        {
                            var stack = currentStacks[i];
                            var boxes = stack.Children.OfType<TextBox>().ToList();
                            if (boxes.Count >= 2)
                            {
                                boxes[0].Text = data.Layers[i].Points;
                                boxes[1].Text = data.Layers[i].Diameter;
                            }
                        }
                    }

                    WindHInput.Text = data.WindH;
                    WindS1Input.Text = data.WindS1;
                    WindS2Input.Text = data.WindS2;
                    WindS3Input.Text = data.WindS3;
                    WindV0Input.Text = data.WindV0;
                    WindCaInput.Text = data.WindCa;
                    PesoReservatorioInput.Text = data.PesoReservatorio;
                    VolumeReservatorioInput.Text = data.VolumeReservatorio;
                    PesoBlocoInput.Text = data.PesoBloco;
                    MomentoBaseInput.Text = data.MomentoBase;
                    ForcaHorizontalInput.Text = data.ForcaHorizontal;
                    ConsiderPbCheckbox.IsChecked = data.ConsiderPb;

                    _currentFilePath = path;
                    UpdateTitle();

                    // Navigate to Config after loading (ALWAYS)
                    WelcomeView.Visibility = Visibility.Collapsed;
                    ChoiceView.Visibility = Visibility.Collapsed;
                    WindInputView.Visibility = Visibility.Collapsed;
                    WindResultView.Visibility = Visibility.Collapsed;
                    LoadDataView.Visibility = Visibility.Collapsed;
                    ResultsView.Visibility = Visibility.Collapsed;
                    
                    ConfigView.Visibility = Visibility.Visible;
                    
                    DrawPreview();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetInputs()
        {
            WindHInput.Text = "";
            WindS1Input.Text = "1,0";
            WindS2Input.Text = "1,0";
            WindS3Input.Text = "1,0";
            WindV0Input.Text = "";
            WindCaInput.Text = "";
            PesoReservatorioInput.Text = "";
            VolumeReservatorioInput.Text = "";
            PesoBlocoInput.Text = "";
            MomentoBaseInput.Text = "";
            ForcaHorizontalInput.Text = "";
            ConsiderPbCheckbox.IsChecked = false;
            
            LayersCountInput.Text = "1";
            UpdateLayersUI();
        }

        private void UpdateTitle()
        {
            string fileName = string.IsNullOrEmpty(_currentFilePath) ? "Novo Projeto" : System.IO.Path.GetFileName(_currentFilePath);
            this.Title = $"D2BASE - {fileName}";
        }

        // --- Navigation ---

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            WelcomeView.Visibility = Visibility.Collapsed;
            ConfigView.Visibility = Visibility.Visible;
            if (LayersContainer.Children.Count == 0) UpdateLayersUI();
            UpdateLayout();
            DrawPreview();
        }

        private void GoToChoiceBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateConfigInputs())
            {
                ConfigView.Visibility = Visibility.Collapsed;
                ChoiceView.Visibility = Visibility.Visible;
            }
        }

        private void EnterActionsBtn_Click(object sender, RoutedEventArgs e)
        {
            ChoiceView.Visibility = Visibility.Collapsed;
            LoadDataView.Visibility = Visibility.Visible;
        }

        private void WindDataBtn_Click(object sender, RoutedEventArgs e)
        {
            ChoiceView.Visibility = Visibility.Collapsed;
            WindInputView.Visibility = Visibility.Visible;
        }

        private void BackFromChoiceBtn_Click(object sender, RoutedEventArgs e)
        {
            ChoiceView.Visibility = Visibility.Collapsed;
            ConfigView.Visibility = Visibility.Visible;
        }

        private void BackToChoiceBtn_Click(object sender, RoutedEventArgs e)
        {
            WindInputView.Visibility = Visibility.Collapsed;
            ChoiceView.Visibility = Visibility.Visible;
        }

        private void CalculateWindBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateWindInputs())
            {
                PerformWindCalculations();
                WindInputView.Visibility = Visibility.Collapsed;
                WindResultView.Visibility = Visibility.Visible;
            }
        }

        private void LayersCountInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLayersUI();
        }

        private void UpdateLayersUI()
        {
            if (LayersContainer == null) return;
            
            if (!int.TryParse(LayersCountInput.Text, out int count) || count < 1)
            {
                count = 1;
            }

            if (count > 20) count = 20;

            int currentCount = LayersContainer.Children.Count;

            if (count > currentCount)
            {
                for (int i = currentCount; i < count; i++)
                {
                    AddLayerInput(i + 1);
                }
            }
            else if (count < currentCount)
            {
                for (int i = currentCount - 1; i >= count; i--)
                {
                    LayersContainer.Children.RemoveAt(i);
                }
            }
            
            DrawPreview();
        }

        private void AddLayerInput(int layerIndex)
        {
            var layerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            
            var title = new TextBlock 
            { 
                Text = $"Camada {layerIndex}", 
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284c7")),
                Margin = new Thickness(0, 0, 0, 5)
            };
            
            var pointsLabel = new TextBlock { Text = "Número de Pontos", Style = (Style)FindResource("InputLabelStyle") };
            var pointsInput = new TextBox { Text = "4", Style = (Style)FindResource("InputTextBoxStyle"), Name = $"PointsInput_{layerIndex}" };
            pointsInput.TextChanged += (s, e) => DrawPreview();

            var diameterLabel = new TextBlock { Text = "Diâmetro (m)", Style = (Style)FindResource("InputLabelStyle") };
            var diameterInput = new TextBox { Text = (2 * layerIndex).ToString(), Style = (Style)FindResource("InputTextBoxStyle"), Name = $"DiameterInput_{layerIndex}" };
            diameterInput.TextChanged += (s, e) => DrawPreview();

            layerStack.Children.Add(title);
            layerStack.Children.Add(pointsLabel);
            layerStack.Children.Add(pointsInput);
            layerStack.Children.Add(diameterLabel);
            layerStack.Children.Add(diameterInput);

            LayersContainer.Children.Add(layerStack);
        }

        private List<LayerData> GetLayersFromUI()
        {
            var layers = new List<LayerData>();
            if (LayersContainer == null) return layers;

            foreach (var child in LayersContainer.Children)
            {
                if (child is StackPanel stack)
                {
                    var boxes = stack.Children.OfType<TextBox>().ToList();
                    if (boxes.Count >= 2)
                    {
                        layers.Add(new LayerData { Points = boxes[0].Text, Diameter = boxes[1].Text });
                    }
                }
            }
            return layers;
        }

        private void BackToWindInputBtn_Click(object sender, RoutedEventArgs e)
        {
            WindResultView.Visibility = Visibility.Collapsed;
            WindInputView.Visibility = Visibility.Visible;
        }

        private void UseWindCalculationsBtn_Click(object sender, RoutedEventArgs e)
        {
            WindResultView.Visibility = Visibility.Collapsed;
            LoadDataView.Visibility = Visibility.Visible;

            MomentoBaseInput.Text = _calculatedM.ToString("F3");
            ForcaHorizontalInput.Text = _calculatedFa.ToString("F3");
        }

        private void BackToConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadDataView.Visibility = Visibility.Collapsed;
            ConfigView.Visibility = Visibility.Visible;
        }

        private void CalculateBtn_Click(object sender, RoutedEventArgs e)
        {
            PerformCalculations();
            LoadDataView.Visibility = Visibility.Collapsed;
            ResultsView.Visibility = Visibility.Visible;
            DrawResults(); 
        }

        private void BackToLoadsBtn_Click(object sender, RoutedEventArgs e)
        {
            ResultsView.Visibility = Visibility.Collapsed;
            LoadDataView.Visibility = Visibility.Visible;
        }

        private void RestartBtn_Click(object sender, RoutedEventArgs e)
        {
            ResultsView.Visibility = Visibility.Collapsed;
            WelcomeView.Visibility = Visibility.Visible;
        }

        // --- Logic & Validation ---

        private bool ValidateConfigInputs()
        {
            if (!int.TryParse(LayersCountInput.Text, out int layersCount) || layersCount < 1)
            {
                MessageBox.Show("Por favor, insira um número válido de camadas (mínimo 1).", "Entrada Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var layers = GetLayersFromUI();
            foreach (var layer in layers)
            {
                if (!int.TryParse(layer.Points, out int points) || points < 2 ||
                    !double.TryParse(layer.Diameter, out double diameter) || diameter <= 0)
                {
                    MessageBox.Show("Por favor, insira parâmetros válidos em todas as camadas:\n- Pontos: Inteiro >= 2\n- Diâmetro: Número > 0", "Entrada Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            return true;
        }

        private bool ValidateWindInputs()
        {
            bool hOk = double.TryParse(WindHInput.Text, out _);
            bool s1Ok = double.TryParse(WindS1Input.Text, out _);
            bool s2Ok = double.TryParse(WindS2Input.Text, out _);
            bool s3Ok = double.TryParse(WindS3Input.Text, out _);
            bool v0Ok = double.TryParse(WindV0Input.Text, out _);
            bool caOk = double.TryParse(WindCaInput.Text, out _);

            if (hOk && s1Ok && s2Ok && s3Ok && v0Ok && caOk) return true;

            MessageBox.Show("Por favor, preencha todos os campos com valores numéricos válidos.", "Entrada Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfigView.Visibility == Visibility.Visible)
            {
                DrawPreview();
            }
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ConfigView.Visibility == Visibility.Visible) DrawPreview();
            if (ResultsView.Visibility == Visibility.Visible) DrawResults();
        }

        // --- Drawing ---

        private void DrawPreview()
        {
            if (PreviewCanvas.ActualWidth == 0 || PreviewCanvas.ActualHeight == 0) return;

            PreviewCanvas.Children.Clear();

            var layers = GetLayersFromUI();
            if (layers.Count == 0) return;

            double maxDiameter = 0;
            foreach (var layer in layers)
            {
                if (double.TryParse(layer.Diameter, out double d) && d > maxDiameter)
                    maxDiameter = d;
            }

            if (maxDiameter <= 0) maxDiameter = 2; // Default fallback for scaling

            int nextPointId = 1;
            foreach (var layer in layers)
            {
                if (int.TryParse(layer.Points, out int numPoints) && double.TryParse(layer.Diameter, out double diameter))
                {
                    DrawGeneric(PreviewCanvas, numPoints, diameter, maxDiameter, false, ref nextPointId);
                }
            }
        }

        private void DrawResults()
        {
            if (ResultsCanvas.ActualWidth == 0 || ResultsCanvas.ActualHeight == 0) return;

            ResultsCanvas.Children.Clear();

            var layers = GetLayersFromUI();
            if (layers.Count == 0) return;

            double maxDiameter = 0;
            foreach (var layer in layers)
            {
                if (double.TryParse(layer.Diameter, out double d) && d > maxDiameter)
                    maxDiameter = d;
            }

            if (maxDiameter <= 0) maxDiameter = 2;

            int nextPointId = 1;
            foreach (var layer in layers)
            {
                if (int.TryParse(layer.Points, out int numPoints) && double.TryParse(layer.Diameter, out double diameter))
                {
                    DrawGeneric(ResultsCanvas, numPoints, diameter, maxDiameter, true, ref nextPointId);
                }
            }
        }

        private void DrawGeneric(Canvas canvas, int numPoints, double layerDiameter, double maxDiameter, bool isResultMode, ref int nextPointId)
        {
            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            Point center = new Point(width / 2, height / 2);
            
            // Normalize radius relative to the largest circle
            double maxRadiusPx = (Math.Min(width, height) / 2) * 0.8;
            double radiusPx = maxRadiusPx * (layerDiameter / maxDiameter);

            // Draw Circle
            Ellipse circle = new Ellipse
            {
                Width = radiusPx * 2,
                Height = radiusPx * 2,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cbd5e1")),
                StrokeThickness = 1
            };
            Canvas.SetLeft(circle, center.X - radiusPx);
            Canvas.SetTop(circle, center.Y - radiusPx);
            canvas.Children.Add(circle);

            // Calculate Points locations
            List<Point> linePoints = new List<Point>();
            double angleStep = (2 * Math.PI) / numPoints;

            for (int i = 0; i < numPoints; i++)
            {
                double angle = i * angleStep - Math.PI / 2;
                double pxX = center.X + radiusPx * Math.Cos(angle);
                double pxY = center.Y + radiusPx * Math.Sin(angle);

                Point pt = new Point(pxX, pxY);
                linePoints.Add(pt);

                // Draw Point Marker
                double pointSize = isResultMode ? 10 : 6;
                Ellipse pointMarker = new Ellipse
                {
                    Width = pointSize,
                    Height = pointSize,
                    Fill = (SolidColorBrush)FindResource("PrimaryColorCanvas")
                };
                Canvas.SetLeft(pointMarker, pxX - pointSize / 2);
                Canvas.SetTop(pointMarker, pxY - pointSize / 2);
                canvas.Children.Add(pointMarker);

                // Draw Number in Results Mode
                if (isResultMode)
                {
                    double textOffset = 20;
                    double textX = center.X + (radiusPx + textOffset) * Math.Cos(angle);
                    double textY = center.Y + (radiusPx + textOffset) * Math.Sin(angle);

                    TextBlock txt = new TextBlock
                    {
                        Text = nextPointId.ToString(),
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0f172a")),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold
                    };
                    
                    txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(txt, textX - txt.DesiredSize.Width / 2);
                    Canvas.SetTop(txt, textY - txt.DesiredSize.Height / 2);
                    canvas.Children.Add(txt);
                }
                
                nextPointId++;
            }

            // Connect Lines
            Polyline polyline = new Polyline
            {
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cbd5e1")),
                StrokeThickness = 0.5,
                Opacity = 0.5
            };
            foreach (var pt in linePoints)
            {
                polyline.Points.Add(pt);
            }
            if (linePoints.Count > 0)
                polyline.Points.Add(linePoints[0]);
            
            canvas.Children.Add(polyline);
        }

        // --- Calculations ---

        private void PerformWindCalculations()
        {
            double.TryParse(WindHInput.Text, out double h);
            double.TryParse(WindS1Input.Text, out double s1);
            double.TryParse(WindS2Input.Text, out double s2);
            double.TryParse(WindS3Input.Text, out double s3);
            double.TryParse(WindV0Input.Text, out double v0);
            double.TryParse(WindCaInput.Text, out double ca);
            
            var layers = GetLayersFromUI();
            double d = 0;
            foreach (var layer in layers)
            {
                if (double.TryParse(layer.Diameter, out double layerD) && layerD > d)
                    d = layerD;
            }
            if (d <= 0) d = 2; // Fallback

            // Vk = S1 * S2 * S3 * V0
            double vk = s1 * s2 * s3 * v0;

            // q = 0,613 * Vk^2
            double q = 0.613 * Math.Pow(vk, 2);

            // Fa = Ca * q * h * d / 10000
            double fa = ca * q * h * d / 10000;

            // M = Fa * h / 2
            double m = fa * h / 2;

            _calculatedFa = fa;
            _calculatedM = m;

            // Update UI
            VkResultText.Text = $"{vk:F2} m/s";
            QResultText.Text = $"{q:F2} N/m²";
            FaResultText.Text = $"{fa:F3} tf";
            MResultText.Text = $"{m:F3} tf.m";
        }

        private void PerformCalculations()
        {
            // Parse Inputs
            double.TryParse(PesoReservatorioInput.Text, out double P1);
            double.TryParse(VolumeReservatorioInput.Text, out double V);
            double.TryParse(PesoBlocoInput.Text, out double Pb);
            double.TryParse(MomentoBaseInput.Text, out double M);
            double.TryParse(ForcaHorizontalInput.Text, out double H_Total);
            bool considerPb = ConsiderPbCheckbox.IsChecked == true;

            var layers = GetLayersFromUI();
            int totalPoints = 0;
            double totalInertia = 0;

            foreach (var layer in layers)
            {
                if (int.TryParse(layer.Points, out int pts) && double.TryParse(layer.Diameter, out double diam))
                {
                    totalPoints += pts;
                    double radiusM = diam / 2;
                    totalInertia += (pts * Math.Pow(radiusM, 2)) / 2;
                }
            }

            if (totalPoints == 0) return;

            _calculatedPoints.Clear();

            double N1 = (P1 + V + Pb) / totalPoints;
            double hPerPoint = (totalPoints > 0) ? (H_Total / totalPoints) : 0;

            // Prepare for Canvas mapping (to store CanvasX/Y for tooltip)
            double canvasWidth = ResultsCanvas.ActualWidth;
            double canvasHeight = ResultsCanvas.ActualHeight;
            double centerX = canvasWidth / 2;
            double centerY = canvasHeight / 2;
            
            double maxDiameter = 0;
            foreach (var layer in layers)
            {
                if (double.TryParse(layer.Diameter, out double d) && d > maxDiameter)
                    maxDiameter = d;
            }
            if (maxDiameter <= 0) maxDiameter = 2;
            double maxRadiusPx = (Math.Min(canvasWidth, canvasHeight) / 2) * 0.8;

            int globalPointId = 1;
            foreach (var layer in layers)
            {
                if (int.TryParse(layer.Points, out int numPoints) && double.TryParse(layer.Diameter, out double diameter))
                {
                    double radiusMeters = diameter / 2;
                    double radiusPx = maxRadiusPx * (diameter / maxDiameter);
                    double angleStep = (2 * Math.PI) / numPoints;

                    for (int i = 0; i < numPoints; i++)
                    {
                        double angle = i * angleStep - Math.PI / 2;

                        double realX = Math.Cos(angle) * radiusMeters;
                        double realY = -Math.Sin(angle) * radiusMeters;

                        double Rm = (totalInertia != 0) ? (M * realY / totalInertia) : 0;

                        double nmax = N1 + Rm;
                        double nmin = 0;

                        if (considerPb)
                        {
                            nmin = ((P1 + Pb) / totalPoints) + Rm;
                        }
                        else
                        {
                            nmin = (P1 / totalPoints) + Rm;
                        }

                        // Canvas Coords for Tooltip
                        double pxX = centerX + radiusPx * Math.Cos(angle);
                        double pxY = centerY + radiusPx * Math.Sin(angle);

                        _calculatedPoints.Add(new ResultPoint
                        {
                            Id = globalPointId++,
                            RealX = realX,
                            RealY = realY,
                            Nmax = nmax,
                            Nmin = nmin,
                            H = hPerPoint,
                            CanvasX = pxX,
                            CanvasY = pxY
                        });
                    }
                }
            }

            if (_calculatedPoints.Count > 0)
            {
                double maxN = _calculatedPoints.Max(p => p.Nmax);
                double minN = _calculatedPoints.Min(p => p.Nmin);

                foreach (var p in _calculatedPoints)
                {
                    p.NmaxFontWeight = (p.Nmax == maxN) ? FontWeights.Bold : FontWeights.Normal;
                    p.NminFontWeight = (p.Nmin == minN) ? FontWeights.Bold : FontWeights.Normal;
                }
            }

            ResultsGrid.ItemsSource = null; // refresh
            ResultsGrid.ItemsSource = _calculatedPoints;
        }

        // --- Tooltip Logic ---

        private void ResultsCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(ResultsCanvas);
            double threshold = 20;

            var hovered = _calculatedPoints.FirstOrDefault(p => 
                GetDistance(mousePos.X, mousePos.Y, p.CanvasX, p.CanvasY) < threshold);

            if (hovered != null)
            {
                GraphTooltip.IsOpen = true;
                TooltipText.Text = $"Ponto {hovered.Id}\n" +
                                   $"X: {hovered.RealX:F3} m\n" +
                                   $"Y: {hovered.RealY:F3} m\n" +
                                   $"-------------\n" +
                                   $"Nmáx: {hovered.Nmax:F3} tf\n" +
                                   $"Nmín: {hovered.Nmin:F3} tf\n" +
                                   $"H: {hovered.H:F3} tf";
            }
            else
            {
                GraphTooltip.IsOpen = false;
            }
        }

        private void ResultsCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            GraphTooltip.IsOpen = false;
        }

        private double GetDistance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
        }
    }
}