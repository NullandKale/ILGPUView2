using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GPU
{
    public static class UIBuilder
    {
        private static Grid rootGrid;
        private static StackPanel sidebarStackPanel;
        private static Grid contentGrid; // This will hold the content view
        private static Action<string> onFPSUpdateCallback;

        public static void SetFPSCallback(Action<string> onFPSUpdate)
        {
            onFPSUpdateCallback = onFPSUpdate;
        }

        public static void OnFPSUpdate(string FPS)
        {
            onFPSUpdateCallback?.Invoke(FPS);
        }

        public static void SetUIGrid(Grid uiGrid)
        {
            // Use the provided Grid as the root grid
            rootGrid = uiGrid;

            // Define two columns: one for the sidebar and one for the content view
            rootGrid.ColumnDefinitions.Clear();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) }); // Sidebar width
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Content area (starts hidden)

            // Initialize the sidebar StackPanel
            sidebarStackPanel = new StackPanel();
            ScrollViewer sidebarScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = sidebarStackPanel
            };
            Grid.SetColumn(sidebarScrollViewer, 0);
            rootGrid.Children.Add(sidebarScrollViewer);

            // Initialize the content grid, hidden at the start
            contentGrid = new Grid();
            Grid.SetColumn(contentGrid, 1);
            rootGrid.Children.Add(contentGrid);

            // Start with the sidebar filling the entire grid (because content is initially empty)
            CollapseContentArea();
        }

        // Internal method to set the content view
        public static void SetContentView(System.Windows.UIElement contentView)
        {
            if (contentView == null)
            {
                // If the content is null, collapse the content area and expand the sidebar
                CollapseContentArea();
            }
            else
            {
                // Clear any existing content in the content area
                contentGrid.Children.Clear();

                // Add the new content view
                contentGrid.Children.Add(contentView);

                // Adjust the grid layout to show the content
                ExpandContentArea();
            }
        }

        private static void CollapseContentArea()
        {
            // Adjust the grid so that the sidebar fills the entire grid
            rootGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            rootGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }

        private static void ExpandContentArea()
        {
            // Adjust the grid back to having the sidebar and content
            rootGrid.ColumnDefinitions[0].Width = new GridLength(250); // Sidebar width
            rootGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star); // Content area
        }

        private static void AddChild(System.Windows.UIElement element)
        {
            sidebarStackPanel.Children.Add(element);
        }

        public static void RemoveControl(System.Windows.UIElement element)
        {
            sidebarStackPanel.Children.Remove(element);
        }

        public static void Clear()
        {
            sidebarStackPanel.Children.Clear();
        }

        public static Label AddLabel(string text)
        {
            Label l = new Label();
            l.Content = text;
            AddChild(l);
            return l;
        }

        public static Slider AddSlider(float min, float max, float start, Action<float> callback)
        {
            Slider slider = new Slider();
            slider.Minimum = min;
            slider.Maximum = max;
            slider.Value = start;
            slider.ValueChanged += (sender, e) => { callback((float)e.NewValue); };
            AddChild(slider);
            callback(start);
            return slider;
        }

        public static Slider AddSlider(string prefix, float min, float max, float start, Action<float> callback)
        {
            Label label = UIBuilder.AddLabel("");
            Slider slider = new Slider();
            slider.Minimum = min;
            slider.Maximum = max;
            slider.Value = start;
            slider.ValueChanged += (sender, e) =>
            {
                float val = (float)e.NewValue;
                label.Content = prefix + val.ToString("0.00");
                callback(val);
            };
            AddChild(slider);
            callback(start);
            label.Content = prefix + start.ToString("0.00");
            return slider;
        }

        public static Slider AddSlider(Label label, string prefix, float min, float max, float start, Action<float> callback)
        {
            Slider slider = new Slider();
            slider.Minimum = min;
            slider.Maximum = max;
            slider.Value = start;
            slider.ValueChanged += (sender, e) => 
            { 
                float val = (float)e.NewValue;  
                label.Content = prefix + val.ToString("0.00");
                callback(val); 
            };
            AddChild(slider);
            callback(start);
            label.Content = prefix + start.ToString("0.00");
            return slider;
        }

        public static void AddVec3Slider(Vec3 min, Vec3 max, Vec3 @default, Action<Vec3> callback)
        {
            // Create a StackPanel to hold the sliders and label
            StackPanel panel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            // Create a label to display the current Vec3 values
            Label valueLabel = new Label
            {
                Content = $"Vec3: ({@default.x:0.00}, {@default.y:0.00}, {@default.z:0.00})"
            };
            panel.Children.Add(valueLabel);

            // Variables to hold current values
            float currentX = @default.x;
            float currentY = @default.y;
            float currentZ = @default.z;

            // Function to update the value label and invoke the callback
            void UpdateValue()
            {
                valueLabel.Content = $"Vec3: ({currentX:0.00}, {currentY:0.00}, {currentZ:0.00})";
                callback(new Vec3(currentX, currentY, currentZ));
            }

            // X Slider
            Slider xSlider = new Slider
            {
                Minimum = min.x,
                Maximum = max.x,
                Value = currentX,
                Margin = new Thickness(0, 5, 0, 0)
            };
            xSlider.ValueChanged += (sender, e) =>
            {
                currentX = (float)e.NewValue;
                UpdateValue();
            };
            panel.Children.Add(xSlider);

            // Y Slider
            Slider ySlider = new Slider
            {
                Minimum = min.y,
                Maximum = max.y,
                Value = currentY,
                Margin = new Thickness(0, 5, 0, 0)
            };
            ySlider.ValueChanged += (sender, e) =>
            {
                currentY = (float)e.NewValue;
                UpdateValue();
            };
            panel.Children.Add(ySlider);

            // Z Slider
            Slider zSlider = new Slider
            {
                Minimum = min.z,
                Maximum = max.z,
                Value = currentZ,
                Margin = new Thickness(0, 5, 0, 0)
            };
            zSlider.ValueChanged += (sender, e) =>
            {
                currentZ = (float)e.NewValue;
                UpdateValue();
            };
            panel.Children.Add(zSlider);

            // Add the panel containing all elements to the UI
            AddChild(panel);

            // Invoke the callback with the default value
            UpdateValue();
        }


        public static void AddColorSliderWithPreview(Action<Vec3> callback)
        {
            // Create a Rectangle for color preview
            Rectangle colorPreview = new Rectangle();
            colorPreview.Height = 20;
            colorPreview.Width = 100;
            colorPreview.Stroke = new SolidColorBrush(Colors.Black);
            colorPreview.Margin = new Thickness(5);

            // Add the color preview to the panel
            AddChild(colorPreview);

            Slider hueSlider = null;
            Slider saturationSlider = null;
            Slider brightnessSlider = null;

            // Hue Slider
            hueSlider = AddSlider("Hue: ", 0, 360, 0, (hue) =>
            {
                if(hueSlider != null && saturationSlider != null && brightnessSlider != null)
                {
                    UpdateHSBColor(hueSlider, saturationSlider, brightnessSlider, colorPreview, callback);
                }
            });

            // Saturation Slider
            saturationSlider = AddSlider("Saturation: ", 0, 1, 0.5f, (sat) =>
            {
                if (hueSlider != null && saturationSlider != null && brightnessSlider != null)
                {
                    UpdateHSBColor(hueSlider, saturationSlider, brightnessSlider, colorPreview, callback);
                }
            });

            // Brightness Slider
            brightnessSlider = AddSlider("Brightness: ", 0, 1, 0.5f, (bri) =>
            {
                if (hueSlider != null && saturationSlider != null && brightnessSlider != null)
                {
                    UpdateHSBColor(hueSlider, saturationSlider, brightnessSlider, colorPreview, callback);
                }
            });
        }

        private static void UpdateHSBColor(Slider hueSlider, Slider satSlider, Slider briSlider, Rectangle colorPreview, Action<Vec3> callback)
        {
            float hue = (float)hueSlider.Value;
            float saturation = (float)satSlider.Value;
            float brightness = (float)briSlider.Value;

            Vec3 hsbColor = new Vec3(hue / 360f, saturation, brightness);
            Vec3 rgb = Vec3.HsbToRgb(hsbColor);
            callback(rgb);

            // Update the color preview
            colorPreview.Fill = new SolidColorBrush(Color.FromRgb((byte)(rgb.x * 255), (byte)(rgb.y * 255), (byte)(rgb.z * 255)));
        }

        public static Button AddButton(string name, Action onPressed)
        {
            Button button = new Button();
            button.Content = name;
            button.Click += (sender, e) => { onPressed(); };
            AddChild(button);
            return button;
        }

        public static ComboBox AddDropdown(string[] items, Action<int> onItemSelected)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.ItemsSource = items;
            comboBox.SelectedIndex = 0;

            comboBox.SelectionChanged += (sender, args) =>
            {
                if (comboBox.SelectedIndex != -1)
                {
                    onItemSelected(comboBox.SelectedIndex);
                }
            };

            AddChild(comboBox);

            return comboBox;
        }

        public static ComboBox AddDropdown(string[] items, int defaultItem, Action<int> onItemSelected)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.ItemsSource = items;
            comboBox.SelectedIndex = defaultItem;

            comboBox.SelectionChanged += (sender, args) =>
            {
                if (comboBox.SelectedIndex != -1)
                {
                    onItemSelected(comboBox.SelectedIndex);
                }
            };

            AddChild(comboBox);

            return comboBox;
        }

        public static TextBox AddFloatTextBox(float min, float max, float @default, Action<float> onTextUpdated)
        {
            var textBox = new TextBox
            {
                Text = @default.ToString(CultureInfo.InvariantCulture),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            textBox.TextChanged += (sender, args) =>
            {
                if (float.TryParse(textBox.Text, out var value))
                {
                    value = Math.Clamp(value, min, max);
                    onTextUpdated(value);
                }
            };

            AddChild(textBox);

            return textBox;
        }

        public static TextBox AddTextBox(string defaultText, Action<string> onTextChanged)
        {
            TextBox textBox = new TextBox();
            textBox.Text = defaultText;
            textBox.TextChanged += (sender, e) => { onTextChanged(textBox.Text); };
            AddChild(textBox);
            return textBox;
        }

        public static StackPanel AddTextBoxAndBrowseButton(string defaultText, Action<string> onTextChanged)
        {
            StackPanel panel = new StackPanel();
            panel.Orientation = Orientation.Horizontal;

            TextBox textBox = new TextBox();
            textBox.Text = defaultText;
            textBox.Width = 200;
            textBox.TextChanged += (sender, e) => { onTextChanged(textBox.Text); };
            panel.Children.Add(textBox);

            Button browseButton = new Button();
            browseButton.Content = "Browse";
            browseButton.Width = 80;
            browseButton.Click += (sender, e) => {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.FileName = "";
                dlg.DefaultExt = ".txt";
                dlg.Filter = "Text documents (.txt)|*.txt";

                Nullable<bool> result = dlg.ShowDialog();

                if (result == true)
                {
                    textBox.Text = dlg.FileName;
                    onTextChanged(textBox.Text);
                }
            };
            panel.Children.Add(browseButton);

            AddChild(panel);

            return panel;
        }

        public static TextBox AddIntTextBox(int min, int max, int @default, Action<int> onTextUpdated)
        {
            var textBox = new TextBox
            {
                Text = @default.ToString(),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            textBox.TextChanged += (sender, args) =>
            {
                if (int.TryParse(textBox.Text, out var value))
                {
                    value = Math.Clamp(value, min, max);
                    onTextUpdated(value);
                }
            };

            AddChild(textBox);

            return textBox;
        }

        public static TextBox AddFilePicker(string buttonText, string textBoxDefaultText, string fileFilter, Action<string> onNewFile)
        {
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBox textBox = new TextBox();
            textBox.Margin = new Thickness(10);
            textBox.VerticalContentAlignment = VerticalAlignment.Center;
            textBox.Height = 30;
            textBox.Text = textBoxDefaultText;
            textBox.TextChanged += (sender, args) =>
            {
                onNewFile(textBox.Text);
            };
            Grid.SetRow(textBox, 0);
            Grid.SetColumn(textBox, 1);

            Button button = new Button();
            button.Content = buttonText;
            button.Margin = new Thickness(10);
            button.Click += (sender, e) =>
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.DefaultExt = ".png";
                dlg.Filter = fileFilter;

                Nullable<bool> result = dlg.ShowDialog();
                if (result == true)
                {
                    textBox.Text = dlg.FileName;
                }
            };
            Grid.SetRow(button, 0);
            Grid.SetColumn(button, 0);

            grid.Children.Add(button);
            grid.Children.Add(textBox);

            AddChild(grid);

            return textBox;
        }
    }
}