#ifndef DARK1C_TEXT_CONTRAST_H
#define DARK1C_TEXT_CONTRAST_H

#include <windows.h>
#include <math.h>

/* Relative sRGB luminance; 4.5:1 is the regression target for normal text.
   https://www.w3.org/TR/WCAG22/#dfn-relative-luminance */
static double textChannel(int value) {
    double c=value/255.;
    return c<=.04045?c/12.92:pow((c+.055)/1.055,2.4);
}
static double textLuminance(COLORREF color) {
    return .2126*textChannel(GetRValue(color))+
           .7152*textChannel(GetGValue(color))+.0722*textChannel(GetBValue(color));
}
static double textContrast(COLORREF foreground,COLORREF background) {
    double f=textLuminance(foreground),b=textLuminance(background);
    return f>b?(f+.05)/(b+.05):(b+.05)/(f+.05);
}
static COLORREF readableText(COLORREF originalFg,COLORREF originalBg,COLORREF mappedFg,COLORREF mappedBg) {
    /* A one-entry per-thread cache avoids repeated pow calls for editor runs.
       Include all four colours so a palette change cannot reuse stale results. */
    static __thread struct {
        int ready;
        COLORREF originalFg,originalBg,mappedFg,mappedBg,result;
    } cache;
    if(cache.ready&&cache.originalFg==originalFg&&cache.originalBg==originalBg&&
       cache.mappedFg==mappedFg&&cache.mappedBg==mappedBg)return cache.result;
    COLORREF result=mappedFg;
    double mappedRatio=textContrast(mappedFg,mappedBg);
    double originalRatio=textContrast(originalFg,originalBg);
    /* Standard Windows white-on-blue is about 4.499:1, just below 4.5.
       Preserve pure white/black text up to its original contrast as well. */
    double target=originalRatio<4.5?originalRatio:4.5;
    if(mappedRatio<target&&(originalRatio>=4.5||originalFg==RGB(255,255,255)||originalFg==RGB(0,0,0))) {
        /* Keep the original text colour when it still fits (e.g. white on blue).
           Leave originally muted/disabled pairs alone rather than promoting them. */
        if(textContrast(originalFg,mappedBg)>=target)result=originalFg;
        else result=textLuminance(mappedBg)>.179?RGB(0,0,0):RGB(255,255,255);
    }
    cache.ready=1;cache.originalFg=originalFg;cache.originalBg=originalBg;
    cache.mappedFg=mappedFg;cache.mappedBg=mappedBg;cache.result=result;
    return result;
}

typedef BOOL (WINAPI *TextDraw)(HDC,int,int,UINT,const RECT*,LPCWSTR,UINT,const INT*);
static BOOL drawReadableText(TextDraw draw,HDC dc,int x,int y,UINT opts,const RECT*rc,
                             LPCWSTR text,UINT n,const INT*dx,COLORREF mappedFg,COLORREF mappedBg) {
    COLORREF fg=GetTextColor(dc),bg=GetBkColor(dc);
    /* With transparent text, GetBkColor is not evidence of the painted surface.
       ETO_OPAQUE fills the supplied rectangle even in TRANSPARENT mode. */
    if(GetBkMode(dc)==OPAQUE||((opts&ETO_OPAQUE)&&rc))
        mappedFg=readableText(fg,bg,mappedFg,mappedBg);
    SetTextColor(dc,mappedFg);SetBkColor(dc,mappedBg);
    BOOL result=draw(dc,x,y,opts,rc,text,n,dx);
    SetTextColor(dc,fg);SetBkColor(dc,bg);
    return result;
}

#endif
