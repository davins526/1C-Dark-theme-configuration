using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Dark1C {
    // Vector-drawn control chrome. Button semantics, keyboard and accessibility
    // remain provided by WinForms; no window regions are changed in 1C.
    sealed class RoundedButton : Button {
        bool hovered,pressed;
        public RoundedButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;}
        protected override void OnMouseEnter(System.EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(System.EventArgs e){hovered=pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            var rect=new RectangleF(1,1,Width-3,Height-3);float diameter=System.Math.Min(20,System.Math.Min(rect.Width,rect.Height));
            if(diameter<=0)return;
            using(var path=new GraphicsPath()){
                path.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);path.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,90);
                path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);path.AddArc(rect.Left,rect.Bottom-diameter,diameter,diameter,90,90);path.CloseFigure();
                using(var brush=new SolidBrush(pressed?Color.FromArgb(65,65,65):hovered?Color.FromArgb(55,55,55):Color.FromArgb(42,42,42)))e.Graphics.FillPath(brush,path);
                using(var pen=new Pen(Focused?Color.FromArgb(200,165,95):Color.FromArgb(78,78,78)))e.Graphics.DrawPath(pen,path);
            }
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(115,115,115),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
    }
}
