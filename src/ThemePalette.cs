using System.Drawing;

namespace Dark1C {
    internal sealed class ThemePalette {
        internal Color Background,Panel,Surface,Text,Muted,Comment,Keyword,Selection,Border;
        internal static ThemePalette FromTheme(int theme) {
            if(theme==1)return new ThemePalette {
                Background=Color.FromArgb(250,250,250),Panel=Color.FromArgb(242,242,242),Surface=Color.FromArgb(230,230,230),
                Text=Color.FromArgb(32,32,32),Muted=Color.FromArgb(100,100,100),Comment=Color.FromArgb(59,111,71),
                Keyword=Color.FromArgb(43,79,139),Selection=Color.FromArgb(221,231,243),Border=Color.FromArgb(185,185,185)};
            return new ThemePalette {
                Background=Color.FromArgb(30,30,30),Panel=Color.FromArgb(37,37,37),Surface=Color.FromArgb(48,48,48),
                Text=Color.FromArgb(218,218,218),Muted=Color.FromArgb(151,151,151),Comment=Color.FromArgb(143,171,141),
                Keyword=Color.FromArgb(163,191,231),Selection=Color.FromArgb(57,68,82),Border=Color.FromArgb(68,68,68)};
        }
    }
}
