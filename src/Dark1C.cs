using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Dark1C {
    sealed class ModulePreview : Control {
        Image wallpaper;
        public int Darkness = 65;
        public bool ShowWallpaper = true;
        int firstLine;
        readonly string[] lines = {
            "// Предпросмотр оформления — это не редактор 1С",
            "Процедура ПриОткрытии(Отказ)",
            "    ОбновитьДанные();",
            "КонецПроцедуры", "",
            "Функция ПолучитьПредставление(Значение)",
            "    Если ЗначениеЗаполнено(Значение) Тогда",
            "        Возврат Строка(Значение);",
            "    КонецЕсли;",
            "    Возврат \"Не указано\";",
            "КонецФункции", "",
            "// Фон закреплён в окне; прокручиваются только строки.",
            "// Ошибки, выделение и остановка должны оставаться заметными."
        };
        public ModulePreview(string image) {
            DoubleBuffered = true; BackColor = Color.FromArgb(30,30,30);
            Font = new Font("Consolas", 12); TabStop = true;
            if(!File.Exists(image))image=Payload.Background;
            if (File.Exists(image)) {
                using (Image original = Image.FromFile(image)) {
                    wallpaper = new Bitmap(1600,1600);
                    using (Graphics g = Graphics.FromImage(wallpaper))
                        g.DrawImage(original,0,0,1600,1600);
                }
            }
        }
        protected override void OnMouseWheel(MouseEventArgs e) {
            firstLine = Math.Max(0,Math.Min(lines.Length-1, firstLine - Math.Sign(e.Delta)*3));
            Invalidate(); base.OnMouseWheel(e);
        }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g=e.Graphics;
            if (ShowWallpaper && wallpaper!=null) {
                float scale=Math.Max((float)Width/wallpaper.Width,(float)Height/wallpaper.Height);
                float w=wallpaper.Width*scale, h=wallpaper.Height*scale;
                g.DrawImage(wallpaper,(Width-w)/2,(Height-h)/2,w,h);
                using (Brush b=new SolidBrush(Color.FromArgb(Darkness*255/100,30,30,30)))
                    g.FillRectangle(b,ClientRectangle);
            }
            using (Brush gutter=new SolidBrush(Color.FromArgb(37,37,37))) g.FillRectangle(gutter,0,0,58,Height);
            int step=28;
            using (Brush current=new SolidBrush(Color.FromArgb(85,70,70,70)))
                g.FillRectangle(current,58,18+step*2,Width-58,step);
            for(int i=firstLine;i<lines.Length;i++) {
                int y=20+(i-firstLine)*step; if(y>Height) break;
                string line=lines[i];
                Color color=line.TrimStart().StartsWith("//")?Color.FromArgb(143,157,141):Color.FromArgb(218,218,218);
                if(line.StartsWith("Процедура")||line.StartsWith("Функция")||line.StartsWith("Конец")) color=Color.FromArgb(160,185,220);
                TextRenderer.DrawText(g,(i+1).ToString(),Font,new Point(12,y),Color.FromArgb(140,140,140),TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g,line,Font,new Point(72,y),color,TextFormatFlags.NoPadding);
            }
        }
        protected override void Dispose(bool disposing) { if(disposing && wallpaper!=null) wallpaper.Dispose(); base.Dispose(disposing); }
    }
    sealed class MainWindow : Form {
        readonly string root=Path.GetDirectoryName(typeof(MainWindow).Assembly.Location);
        ModulePreview preview;
        TrackBar darkness;
        Label amount;
        public MainWindow() {
            Text="Dark1C · предпросмотр темы · без подключения к 1С";
            Size=new Size(1180,780); MinimumSize=new Size(900,620);
            BackColor=Color.FromArgb(37,37,37); ForeColor=Color.Gainsboro;
            Font=new Font("Segoe UI",10); StartPosition=FormStartPosition.CenterScreen;
            var top=new FlowLayoutPanel { Dock=DockStyle.Top, Height=92, Padding=new Padding(12), BackColor=Color.FromArgb(48,48,48), WrapContents=true };
            var image=new CheckBox {Text="Фон модулей",Checked=true,AutoSize=true,Margin=new Padding(8,12,15,0)};
            darkness=new TrackBar { Minimum=0, Maximum=100, Value=65, TickFrequency=10, Width=180, Height=40 };
            amount=new Label { Text="Затемнение: 65%", AutoSize=true,Margin=new Padding(4,12,20,0) };
            var save=Button("Сохранить тему"); var inspect=Button("Проверить платформу…");
            top.Controls.AddRange(new Control[]{image,darkness,amount,save,inspect});
            var status=new Label {Dock=DockStyle.Bottom,Height=48,Padding=new Padding(12,8,0,0),Text="Предпросмотр. Инъекции и изменения 1С отключены. Колесо мыши прокручивает образец кода."};
            var tree=new TreeView {Dock=DockStyle.Left,Width=225,BackColor=BackColor,ForeColor=ForeColor,BorderStyle=BorderStyle.None,ShowLines=false,Indent=18};
            var config=tree.Nodes.Add("Конфигурация");
            foreach(string s in new[]{"Общие","Константы","Справочники","Документы","Регистры сведений","Отчёты","Обработки"}) config.Nodes.Add(s);
            config.Expand();
            var center=new Panel {Dock=DockStyle.Fill,Padding=new Padding(1,0,0,0)};
            var tab=new Label {Dock=DockStyle.Top,Height=36,Text="  ОбщийМодуль · образец оформления",TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.FromArgb(48,48,48)};
            preview=new ModulePreview(Path.Combine(root,"assets","background.jpg")){Dock=DockStyle.Fill};
            center.Controls.Add(preview); center.Controls.Add(tab);
            Controls.Add(center); Controls.Add(tree); Controls.Add(status); Controls.Add(top);
            image.CheckedChanged+=delegate {preview.ShowWallpaper=image.Checked;preview.Invalidate();};
            darkness.ValueChanged+=delegate {preview.Darkness=darkness.Value;amount.Text="Затемнение: "+darkness.Value+"%";preview.Invalidate();};
            save.Click+=delegate {try {
                string path=Path.Combine(root,"theme.ini");
                File.WriteAllText(path,"[palette]\r\nbackground=#1E1E1E\r\npanel=#252525\r\nsurface=#303030\r\ntext=#DADADA\r\n[wallpaper]\r\nfile=assets/background.jpg\r\nenabled="+(image.Checked?"1":"0")+"\r\ndarkness="+darkness.Value+"\r\n",Encoding.UTF8);
                status.Text="Тема сохранена: "+path;
            } catch(Exception ex) {MessageBox.Show(this,ex.Message,"Не удалось сохранить");}};
            inspect.Click+=delegate {using(var dialog=new OpenFileDialog {Filter="Платформа 1С (1cv8.exe)|1cv8.exe|Исполняемые файлы|*.exe",InitialDirectory=@"C:\Program Files\1cv8"}) {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try {MessageBox.Show(this,Inspect(dialog.FileName),"Статическая проверка файла");}
                catch(Exception ex){MessageBox.Show(this,ex.Message,"Ошибка чтения");}
            }};
            string theme=Path.Combine(root,"theme.ini");
            if(File.Exists(theme)) foreach(string line in File.ReadAllLines(theme)) {
                int value;
                if(line.StartsWith("darkness=")&&int.TryParse(line.Substring(9),out value)) darkness.Value=Math.Max(0,Math.Min(100,value));
                if(line=="enabled=0") image.Checked=false;
            }
        }
        Button Button(string text) {return new Button {Text=text,AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(48,48,48),ForeColor=ForeColor,Margin=new Padding(6)};}
        internal static string Inspect(string path) {
            ushort machine;
            using(var reader=new BinaryReader(File.OpenRead(path))) {
                if(reader.ReadUInt16()!=0x5a4d)throw new InvalidDataException("Нет заголовка MZ");
                reader.BaseStream.Position=0x3c; int offset=reader.ReadInt32();
                if(offset<64||offset>reader.BaseStream.Length-6)throw new InvalidDataException("Некорректный заголовок PE");
                reader.BaseStream.Position=offset;
                if(reader.ReadUInt32()!=0x4550)throw new InvalidDataException("Нет заголовка PE");
                machine=reader.ReadUInt16();
            }
            string folder=Path.GetDirectoryName(Path.GetFullPath(path));
            var version=FileVersionInfo.GetVersionInfo(path);
            return "Файл: "+path+"\r\nВерсия: "+version.FileVersion+"\r\nАрхитектура: "+(machine==0x8664?"x64":machine==0x14c?"x86":"0x"+machine.ToString("X4"))+"\r\nCairo рядом: "+(File.Exists(Path.Combine(folder,"cairo.dll"))?"cairo.dll":File.Exists(Path.Combine(folder,"cairo-2.dll"))?"cairo-2.dll":"не найден")+"\r\n\r\nНаличие Cairo не подтверждает совместимость. Файл не запускался, процессы не изменялись.";
        }
        [STAThread] static int Main(string[] args) {
            if(args.Length==2&&args[0]=="--inspect") {
                try {Console.WriteLine(Inspect(args[1]));return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());return 0;
        }
    }
}
