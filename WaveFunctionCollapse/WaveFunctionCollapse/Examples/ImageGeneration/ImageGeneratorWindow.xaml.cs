using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WaveFunctionCollapseAlgorithm.Examples
{
    public partial class ImageGeneratorWindow : Window
    {
        private readonly string _fileFilters = "Image files | *.png";
        private readonly int _defaultSize = 30;
        private readonly int _defaultInterval = 0;
        private readonly int _defaultTileSize = 3;

        private BitmapSource _templateBitmap;
        private BitmapSource _generatedBitmap;
        private Thread _imageGenerationThread;
        private byte[] _generatedBitmapBuffer;

        public ImageGeneratorWindow()
        {
            InitializeComponent();
        }

        private IEnumerable<Control> ControlsToDisable => GetAllControls(this).Where(control => control != _stopButton);
        private bool IsGeneratingImage => _imageGenerationThread is not null && _imageGenerationThread.IsAlive;

        private void OnSaveButtonClick(object sender, RoutedEventArgs e)
        {
            if (_generatedBitmap is null)
                return;

            var saveFileDialog = new SaveFileDialog()
            {
                Title = "Select file name",
                Filter = _fileFilters
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_generatedBitmap));

                using (FileStream stream = File.OpenWrite(saveFileDialog.FileName))
                    encoder.Save(stream);
            }
        }

        private void OnStopButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGeneratingImage)
            {
                _imageGenerationThread.Abort();

                _generatedBitmap = null;
                _generatedImage.Source = null;

                EnableControls();
                ResetContradictionsCount();
            }
        }

        private void OnGenerateButtonClick(object sender, RoutedEventArgs e)
        {
            if (_templateBitmap is null)
                return;

            int tileSize = GetTextBoxValue(_tileSizeTextBox, _defaultTileSize, 1);

            if (tileSize > _templateBitmap.PixelWidth || tileSize > _templateBitmap.PixelHeight)
            {
                MessageBox.Show("Tile size cannot be bigger that any of the template image dimensions", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            DisableControls();
            ResetContradictionsCount();

            Color[,] templateColors = BitmapUtilities.ExtractColors(_templateBitmap);
            int seed = GetSeed();

            var options = new WaveFunctionCollapseOptions()
            {
                Rotation = CheckBoxIsChecked(_rotationCheckBox),
                PeriodicInputDimensions = GetCheckedIndices(_periodicInputXCheckBox, _periodicInputYCheckBox),
                ReflectedDimensions = GetCheckedIndices(_reflectionXCheckBox, _reflectionYCheckBox),
            };

            int outputWidth = GetTextBoxValue(_widthTextBox, _defaultSize, 1);
            int outputHeight = GetTextBoxValue(_heightTextBox, _defaultSize, 1);

            var outputSize = new int[]
            {
                outputWidth,
                outputHeight
            };

            bool drawUncollapsedValues = CheckBoxIsChecked(_drawUncollapsedValuesCheckBox);
            int interval = GetTextBoxValue(_minIntervalCheckBox, _defaultInterval);
            int contradictionsCount = 0;

            IEnumerable<int> periodicDimensions = GetCheckedIndices(_periodicOutputXCheckBox, _periodicOutputYCheckBox);

            _imageGenerationThread = new Thread(() =>
            {
                var waveFunctionCollapse = new WaveFunctionCollapse<Color>(templateColors, 2, tileSize, options);
                waveFunctionCollapse.Prepare(outputSize, seed, periodicDimensions);

                Color defaultColor = drawUncollapsedValues ? BitmapUtilities.GetAverageColor(waveFunctionCollapse.GetAllPossibleValues()) : Colors.Transparent;
                _generatedBitmapBuffer = BitmapUtilities.FillBuffer(outputWidth, outputHeight, defaultColor);

                Dispatcher.Invoke(() =>
                {
                    UpdateGeneratedImage(outputWidth, outputHeight);
                });

                while (true)
                {
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();

                    int contradictions = waveFunctionCollapse.IterateUntilSuccess(out IEnumerable<Element<Color>> changedElements);
                    IEnumerable<Pixel> pixels = drawUncollapsedValues ? GetAveragePixels(changedElements) : GetDeterminedPixels(changedElements);
                    BitmapUtilities.WritePixelsToBuffer(outputWidth, _generatedBitmapBuffer, pixels);

                    BitmapSource bitmap = CreateBitmap(outputWidth, outputHeight);
                    bitmap.Freeze();

                    Dispatcher.Invoke(() =>
                    {
                        UpdateGeneratedImage(bitmap);

                        if (contradictions > 0)
                        {
                            contradictionsCount += contradictions;

                            _contradictionsTextBlock.Text = contradictionsCount.ToString();
                        }
                    });

                    if (waveFunctionCollapse.IsFullyCollapsed)
                    {
                        _generatedBitmap = bitmap;
                        break;
                    }

                    int difference = interval - (int)stopWatch.ElapsedMilliseconds;

                    if (difference > 0)
                        Thread.Sleep(difference);
                }

                Dispatcher.Invoke(EnableControls);
            })
            {
                Priority = ThreadPriority.Highest
            };

            _imageGenerationThread.Start();
        }

        private void OnSelectFileButtonClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog()
            {
                Title = "Select image",
                Filter = _fileFilters
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var uri = new Uri(openFileDialog.FileName);

                _templateBitmap = new BitmapImage(uri);
                _templateImage.Source = _templateBitmap;
            }
        }

        private void OnTextBoxPreviewTextInput(object sender, TextCompositionEventArgs eventArgs)
        {
            eventArgs.Handled = !int.TryParse(eventArgs.Text, out _);
        }

        private void OnClose(object sender, EventArgs e)
        {
            if (IsGeneratingImage)
                _imageGenerationThread.Abort();
        }

        private int GetTextBoxValue(TextBox textBox, int defaultValue, int minValue = 0)
        {
            string text = textBox.Text;

            if (string.IsNullOrEmpty(text))
                return defaultValue;

            int number = int.Parse(text);

            if (number < minValue)
                return minValue;

            return number;
        }

        private IEnumerable<Control> GetAllControls(DependencyObject dependencyObject)
        {
            int controlsCount = VisualTreeHelper.GetChildrenCount(dependencyObject);

            for (int i = 0; i < controlsCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(dependencyObject, i);

                if (child is Control control)
                    yield return control;   

                foreach (Control anotherControl in GetAllControls(child))
                    yield return anotherControl;
            }      
        }

        private void UpdateGeneratedImage(int width, int height)
        {
            BitmapSource bitmap = CreateBitmap(width, height);
            UpdateGeneratedImage(bitmap);
        }

        private void UpdateGeneratedImage(BitmapSource bitmap)
        {
            _generatedImage.Source = bitmap;
        }

        private BitmapSource CreateBitmap(int width, int height)
        {
            return BitmapUtilities.CreateBitmap(width, height, _generatedBitmapBuffer);
        }

        private IEnumerable<Pixel> GetAveragePixels(IEnumerable<Element<Color>> elements)
        {
            foreach (Element<Color> element in elements)
            {
                IEnumerable<int> position = element.Position;
                Color averageColor = BitmapUtilities.GetAverageColor(element.Values);

                yield return new Pixel(averageColor, position.ElementAt(0), position.ElementAt(1));
            }
        }

        private IEnumerable<Pixel> GetDeterminedPixels(IEnumerable<Element<Color>> elements)
        {
            foreach (Element<Color> element in elements)
            {
                IEnumerable<Color> values = element.Values;

                if (values.Count() > 1)
                    continue;

                IEnumerable<int> position = element.Position;

                yield return new Pixel(values.First(), position.ElementAt(0), position.ElementAt(1));
            }
        }

        private void DisableControls()
        {
            foreach (Control control in ControlsToDisable)
                control.IsEnabled = false;
        }

        private void EnableControls()
        {
            foreach (Control control in ControlsToDisable)
                control.IsEnabled = true;
        }

        private bool CheckBoxIsChecked(CheckBox checkBox)
        {
            return checkBox.IsChecked == true;
        }

        private IEnumerable<int> GetCheckedIndices(params CheckBox[] checkBoxes)
        {
            var result = new List<int>();

            for (int i = 0; i < checkBoxes.Length; i++)
                if (CheckBoxIsChecked(checkBoxes[i]))
                    result.Add(i);

            return result;
        }

        private int GetSeed()
        {
            string text = _seedTextBox.Text;    

            if (CheckBoxIsChecked(_randomSeedCheckBox) || string.IsNullOrEmpty(text))
            {
                int seed = new Random().Next();

                _seedTextBox.Text = seed.ToString();
                return seed;
            }

            return int.Parse(text); 
        }

        private void ResetContradictionsCount()
        {
            _contradictionsTextBlock.Text = "0";
        }
    }
}
