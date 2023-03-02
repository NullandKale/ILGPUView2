using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace GPU
{
    public static class UIBuilder
    {
        private static StackPanel instance;
        private static Action<string> onFPSUpdateCallback;
        
        public static void SetFPSCallback(Action<string> onFPSUpdate)
        {
            onFPSUpdateCallback = onFPSUpdate;
        }

        public static void OnFPSUpdate(string FPS)
        {
            if(onFPSUpdateCallback != null)
            {
                onFPSUpdateCallback(FPS);
            }
        }

        public static void SetUIStack(StackPanel stackPanel)
        {
            instance = stackPanel;
        }

        private static void AddChild(System.Windows.UIElement o)
        {
            instance.Children.Add(o);
        }

        public static void Clear()
        {
            instance.Children.Clear();
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

    }
}