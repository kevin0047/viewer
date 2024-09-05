using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.WindowsAPICodePack.Dialogs;
namespace PhotoViewer
{
    public partial class MainWindow : Window
    {
        private string[] imagePaths;
        private List<int> imageOrder;
        private int currentImageIndex = 0;
        private string baseFolder;
        private List<Button> categoryButtons = new List<Button>();
        private Stack<MoveAction> undoStack = new Stack<MoveAction>();
        private Random random = new Random();
        private bool isRandomPlay = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                baseFolder = dialog.FileName;
                LoadImagesFromFolder();
                CreateCategoryButtonsFromSubfolders();
            }
        }

        private void LoadImagesFromFolder()
        {
            try
            {
                imagePaths = Directory.GetFiles(baseFolder, "*.jpg", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(baseFolder, "*.jpeg", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(baseFolder, "*.png", SearchOption.AllDirectories))
                    .ToArray();

                if (imagePaths.Length > 0)
                {
                    SetupImageOrder();
                    currentImageIndex = 0;
                    DisplayImage();
                    MessageBox.Show($"{imagePaths.Length}개의 이미지를 찾았습니다.", "알림");
                }
                else
                {
                    MessageBox.Show("선택한 폴더에 지원되는 이미지 파일이 없습니다.", "알림");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지를 불러오는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateCategoryButtonsFromSubfolders()
        {
            // 기존 카테고리 버튼 제거
            CategoryPanel.Children.Clear();
            categoryButtons.Clear();

            // 하위 폴더를 기반으로 카테고리 버튼 생성
            var subfolders = Directory.GetDirectories(baseFolder);
            foreach (var folder in subfolders)
            {
                string categoryName = Path.GetFileName(folder);
                AddCategoryButton(categoryName);
            }
        }

        private void SetupImageOrder()
        {
            imageOrder = Enumerable.Range(0, imagePaths.Length).ToList();
            if (isRandomPlay)
            {
                imageOrder = imageOrder.OrderBy(x => random.Next()).ToList();
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (imagePaths == null || imagePaths.Length == 0)
            {
                MessageBox.Show("표시할 이미지가 없습니다. 먼저 폴더를 선택해주세요.", "알림");
                return;
            }

            if (currentImageIndex > 0)
            {
                currentImageIndex--;
                DisplayImage();
            }
            else
            {
                MessageBox.Show("이미 첫 번째 이미지입니다.", "알림");
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (imagePaths == null || imagePaths.Length == 0)
            {
                MessageBox.Show("표시할 이미지가 없습니다. 먼저 폴더를 선택해주세요.", "알림");
                return;
            }

            if (currentImageIndex < imageOrder.Count - 1)
            {
                currentImageIndex++;
                DisplayImage();
            }
            else
            {
                MessageBox.Show("이미 마지막 이미지입니다.", "알림");
            }
        }

        private void DisplayImage()
        {
            if (imagePaths != null && imagePaths.Length > 0 && currentImageIndex >= 0 && currentImageIndex < imageOrder.Count)
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePaths[imageOrder[currentImageIndex]]);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    MainImage.Source = bitmap;

                    this.Title = $"Photo Viewer - {Path.GetFileName(imagePaths[imageOrder[currentImageIndex]])} ({currentImageIndex + 1}/{imagePaths.Length})";

                   
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"이미지를 표시하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MainImage.Source = null;
                this.Title = "Photo Viewer";
                
            }
        }



        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TextEntryDialog("새 카테고리 이름을 입력하세요:");
            if (dialog.ShowDialog() == true)
            {
                string categoryName = dialog.ResponseText;
                if (!string.IsNullOrWhiteSpace(categoryName))
                {
                    string newCategoryPath = Path.Combine(baseFolder, categoryName);
                    if (!Directory.Exists(newCategoryPath))
                    {
                        Directory.CreateDirectory(newCategoryPath);
                        AddCategoryButton(categoryName);
                    }
                    else
                    {
                        MessageBox.Show("이미 존재하는 카테고리 이름입니다.", "알림");
                    }
                }
            }
        }

        private void AddCategoryButton(string categoryName)
        {
            Button categoryButton = new Button
            {
                Content = categoryName,
                Margin = new Thickness(5)
            };
            categoryButton.Click += CategoryButton_Click;
            categoryButtons.Add(categoryButton);
            CategoryPanel.Children.Add(categoryButton);
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (imagePaths == null || imagePaths.Length == 0)
            {
                MessageBox.Show("먼저 이미지 폴더를 선택해주세요.");
                return;
            }

            Button clickedButton = (Button)sender;
            string categoryName = clickedButton.Content.ToString();
            string categoryPath = Path.Combine(baseFolder, categoryName);

            if (!Directory.Exists(categoryPath))
            {
                Directory.CreateDirectory(categoryPath);
            }

            string sourceFilePath = imagePaths[imageOrder[currentImageIndex]];
            string fileName = Path.GetFileName(sourceFilePath);
            string destinationFilePath = Path.Combine(categoryPath, fileName);

            try
            {
                File.Move(sourceFilePath, destinationFilePath);

                // 실행 취소 스택에 이동 작업 추가
                undoStack.Push(new MoveAction(destinationFilePath, sourceFilePath));

                // 이동된 이미지를 배열에서 제거
                var tempList = imagePaths.ToList();
                tempList.RemoveAt(imageOrder[currentImageIndex]);
                imagePaths = tempList.ToArray();

                imageOrder.RemoveAt(currentImageIndex);
                if (currentImageIndex >= imageOrder.Count)
                {
                    currentImageIndex = imageOrder.Count - 1;
                }

                if (imagePaths.Length > 0)
                {
                    DisplayImage();
                }
                else
                {
                    MainImage.Source = null;
                    this.Title = "Photo Viewer";
                    MessageBox.Show("모든 이미지가 분류되었습니다.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 이동 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlayOrderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            isRandomPlay = PlayOrderComboBox.SelectedIndex == 1;
            if (imagePaths != null && imagePaths.Length > 0)
            {
                SetupImageOrder();
                DisplayImage();
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 0)
            {
                MoveAction lastMove = undoStack.Pop();
                try
                {
                    File.Move(lastMove.DestinationPath, lastMove.SourcePath);
                    MessageBox.Show($"파일 이동이 취소되었습니다: {System.IO.Path.GetFileName(lastMove.SourcePath)}");

                    // 이미지 배열 및 현재 인덱스 업데이트
                    List<string> imageList = new List<string>(imagePaths);
                    imageList.Insert(currentImageIndex, lastMove.SourcePath);
                    imagePaths = imageList.ToArray();
                    DisplayImage();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"실행 취소 중 오류가 발생했습니다: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("더 이상 취소할 작업이 없습니다.");
            }
        }
    }

    // 이동 작업을 나타내는 클래스
    public class MoveAction
    {
        public string SourcePath { get; }
        public string DestinationPath { get; }

        public MoveAction(string destinationPath, string sourcePath)
        {
            DestinationPath = destinationPath;
            SourcePath = sourcePath;
        }
    }

    // 텍스트 입력을 위한 대화 상자
    public class TextEntryDialog : Window
    {
        private TextBox textBox;
        public string ResponseText { get; private set; }

        public TextEntryDialog(string question)
        {
            Width = 300;
            Height = 150;
            Title = "입력";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var grid = new Grid();
            Content = grid;

            var label = new Label { Content = question, Margin = new Thickness(5) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(label);
            Grid.SetRow(label, 0);

            textBox = new TextBox { Margin = new Thickness(5) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(textBox);
            Grid.SetRow(textBox, 1);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);

            var okButton = new Button { Content = "확인", Width = 75, Margin = new Thickness(5) };
            okButton.Click += (s, e) => { ResponseText = textBox.Text; DialogResult = true; };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new Button { Content = "취소", Width = 75, Margin = new Thickness(5) };
            cancelButton.Click += (s, e) => DialogResult = false;
            buttonPanel.Children.Add(cancelButton);
        }
    }
}