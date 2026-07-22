using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PdfRotateApp
{
    public sealed class MainForm : Form
    {
        private readonly TextBox inputPathTextBox;
        private readonly TextBox outputPathTextBox;
        private readonly TextBox pageRangeTextBox;
        private readonly ComboBox angleComboBox;
        private readonly Label statusLabel;
        private readonly Button rotateButton;

        public MainForm()
        {
            Text = "PDF回転ツール";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 330);
            MinimumSize = new Size(650, 360);
            Font = new Font("Yu Gothic UI", 9F);
            AllowDrop = true;

            var titleLabel = new Label
            {
                Text = "PDF回転ツール",
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 20)
            };

            var inputLabel = CreateLabel("入力PDF", 24, 70);
            inputPathTextBox = CreateTextBox(24, 92, 560);
            var inputButton = CreateButton("参照...", 596, 90, 96);
            inputButton.Click += delegate { SelectInputFile(); };

            var outputLabel = CreateLabel("出力PDF", 24, 130);
            outputPathTextBox = CreateTextBox(24, 152, 560);
            var outputButton = CreateButton("参照...", 596, 150, 96);
            outputButton.Click += delegate { SelectOutputFile(); };

            var rangeLabel = CreateLabel("対象ページ（空欄＝全ページ、例: 1,3,5-10）", 24, 190);
            pageRangeTextBox = CreateTextBox(24, 212, 330);

            var angleLabel = CreateLabel("回転角度", 378, 190);
            angleComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(378, 212),
                Size = new Size(150, 26)
            };
            angleComboBox.Items.AddRange(new object[] {"左へ90°", "右へ90°", "180°"});
            angleComboBox.SelectedIndex = 0;

            rotateButton = CreateButton("回転して保存", 546, 207, 146);
            rotateButton.Height = 34;
            rotateButton.Click += delegate { RotatePdf(); };

            statusLabel = new Label
            {
                Text = "PDFを選択してください。ドラッグ＆ドロップにも対応しています。",
                AutoEllipsis = true,
                Location = new Point(24, 268),
                Size = new Size(668, 38)
            };

            Controls.AddRange(new Control[]
            {
                titleLabel, inputLabel, inputPathTextBox, inputButton,
                outputLabel, outputPathTextBox, outputButton,
                rangeLabel, pageRangeTextBox, angleLabel, angleComboBox,
                rotateButton, statusLabel
            });

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
        }

        private static Label CreateLabel(string text, int x, int y)
        {
            return new Label { Text = text, AutoSize = true, Location = new Point(x, y) };
        }

        private static TextBox CreateTextBox(int x, int y, int width)
        {
            return new TextBox { Location = new Point(x, y), Size = new Size(width, 26) };
        }

        private static Button CreateButton(string text, int x, int y, int width)
        {
            return new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 29) };
        }

        private void SelectInputFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "PDFファイル (*.pdf)|*.pdf|すべてのファイル (*.*)|*.*";
                dialog.Title = "回転するPDFを選択";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SetInputPath(dialog.FileName);
            }
        }

        private void SelectOutputFile()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "PDFファイル (*.pdf)|*.pdf";
                dialog.Title = "保存先を選択";
                dialog.FileName = Path.GetFileName(outputPathTextBox.Text);
                dialog.InitialDirectory = SafeDirectoryName(outputPathTextBox.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    outputPathTextBox.Text = dialog.FileName;
            }
        }

        private void SetInputPath(string path)
        {
            inputPathTextBox.Text = path;
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);
            outputPathTextBox.Text = Path.Combine(directory, name + "_rotated.pdf");
            statusLabel.Text = "入力PDFを選択しました。";
        }

        private void RotatePdf()
        {
            string input = inputPathTextBox.Text.Trim();
            string output = outputPathTextBox.Text.Trim();

            if (!File.Exists(input))
            {
                MessageBox.Show(this, "入力PDFが見つかりません。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(output))
            {
                MessageBox.Show(this, "出力先を指定してください。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "入力ファイルと異なる出力先を指定してください。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            rotateButton.Enabled = false;
            statusLabel.Text = "処理中です...";
            Cursor = Cursors.WaitCursor;

            try
            {
                using (PdfDocument document = PdfReader.Open(input, PdfDocumentOpenMode.Modify))
                {
                    HashSet<int> targetPages = ParsePageRange(pageRangeTextBox.Text, document.PageCount);
                    int delta = GetRotationDelta();

                    for (int i = 0; i < document.PageCount; i++)
                    {
                        int pageNumber = i + 1;
                        if (targetPages != null && !targetPages.Contains(pageNumber)) continue;

                        PdfPage page = document.Pages[i];
                        int current = NormalizeRotation(page.Rotate);
                        page.Rotate = NormalizeRotation(current + delta);
                    }

                    string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(output));
                    if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);
                    document.Save(output);
                }

                statusLabel.Text = "保存しました: " + output;
                if (MessageBox.Show(this, "PDFを保存しました。\n保存先を開きますか？", "完了",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", "/select,\"" + output + "\"");
                }
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, "PDFを処理できませんでした。\n暗号化やPDF形式が原因の可能性があります。\n\n" + ex.Message,
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "処理に失敗しました。";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "処理に失敗しました。";
            }
            finally
            {
                Cursor = Cursors.Default;
                rotateButton.Enabled = true;
            }
        }

        private int GetRotationDelta()
        {
            if (angleComboBox.SelectedIndex == 1) return 90;
            if (angleComboBox.SelectedIndex == 2) return 180;
            return -90;
        }

        private static int NormalizeRotation(int angle)
        {
            int normalized = angle % 360;
            if (normalized < 0) normalized += 360;
            return normalized;
        }

        private static HashSet<int> ParsePageRange(string text, int pageCount)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var result = new HashSet<int>();
            string[] parts = text.Split(',');
            foreach (string rawPart in parts)
            {
                string part = rawPart.Trim();
                if (part.Length == 0) continue;

                int dash = part.IndexOf('-');
                if (dash >= 0)
                {
                    int start = ParsePageNumber(part.Substring(0, dash), pageCount);
                    int end = ParsePageNumber(part.Substring(dash + 1), pageCount);
                    if (start > end) throw new FormatException("ページ範囲の開始が終了より大きくなっています: " + part);
                    for (int page = start; page <= end; page++) result.Add(page);
                }
                else
                {
                    result.Add(ParsePageNumber(part, pageCount));
                }
            }

            if (result.Count == 0) throw new FormatException("対象ページを正しく入力してください。");
            return result;
        }

        private static int ParsePageNumber(string text, int pageCount)
        {
            int value;
            if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 1 || value > pageCount)
                throw new FormatException("ページ番号は1～" + pageCount + "の範囲で入力してください: " + text);
            return value;
        }

        private static string SafeDirectoryName(string path)
        {
            try { return Path.GetDirectoryName(path); }
            catch { return string.Empty; }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            string pdf = files.FirstOrDefault(f => string.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase));
            if (pdf != null) SetInputPath(pdf);
        }
    }
}
