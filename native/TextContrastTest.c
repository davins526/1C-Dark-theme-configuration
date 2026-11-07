#define WIN32_LEAN_AND_MEAN
#include <stdio.h>
#include "TextContrast.h"

static int failures,calls;
static COLORREF observedFg,observedBg;
static BOOL drawResult=TRUE;
static int forwarded;
static const WCHAR sample[]=L"Selection";
static const RECT bounds={0,0,120,30};
static const INT advances[]={10,10,10,10,10,10,10,10,10};
static UINT expectedOptions;
static BOOL WINAPI observeDraw(HDC dc,int x,int y,UINT opts,const RECT*rc,LPCWSTR text,UINT n,const INT*dx) {
    calls++;observedFg=GetTextColor(dc);observedBg=GetBkColor(dc);
    forwarded=x==3&&y==5&&opts==expectedOptions&&rc==&bounds&&text==sample&&n==9&&dx==advances;
    return drawResult;
}
static void check(int condition,const char*message) {
    if(!condition){fprintf(stderr,"FAIL: %s\n",message);failures++;}
}
int main(void) {
    COLORREF white=RGB(255,255,255),black=RGB(0,0,0),dark=RGB(30,30,30),blue=RGB(0,120,215);
    check(fabs(textContrast(black,white)-21)<.00001,"black/white reference ratio");
    check(fabs(textContrast(blue,white)-textContrast(white,blue))<.00001,"contrast symmetry");
    check(textContrast(dark,blue)<4.5,"reproduces the selected-text regression");
    check(readableText(white,blue,dark,blue)==white,"white selection text survives the dark filter");
    COLORREF lightText=RGB(218,218,218),light=RGB(250,250,250);
    check(readableText(black,white,lightText,dark)==lightText,"readable dark palette unchanged");
    check(readableText(black,white,RGB(32,32,32),light)==RGB(32,32,32),"light palette unchanged");
    check(readableText(RGB(128,128,128),white,RGB(95,95,95),dark)==RGB(95,95,95),"muted pair unchanged");
    check(readableText(white,black,light,light)==black,"black fallback on a light surface");
    check(readableText(black,white,dark,dark)==white,"white fallback on a dark surface");
    check(readableText(white,blue,dark,blue)==white,"cache invalidated after pair changes");
    /* Sweep coloured highlight surfaces with an originally readable neutral text.
       Deliberately force an unreadable mapped foreground to exercise both fallbacks. */
    int pairs=0;
    for(int r=0;r<256;r+=17)for(int g=0;g<256;g+=17)for(int b=0;b<256;b+=17) {
        COLORREF bg=RGB(r,g,b);
        check(textContrast(readableText(black,white,bg,bg),bg)>=4.5,"highlight sweep contrast");pairs++;
    }
    /* Private GDI memory DC only: no windows, processes, profiles or 1C bases. */
    HDC dc=CreateCompatibleDC(NULL);
    check(dc!=NULL,"private DC creation");
    if(dc) {
        SetTextColor(dc,white);SetBkColor(dc,blue);SetBkMode(dc,OPAQUE);
        expectedOptions=ETO_CLIPPED;
        check(drawReadableText(observeDraw,dc,3,5,expectedOptions,&bounds,sample,9,advances,dark,blue),"draw result forwarded");
        check(observedFg==white&&observedBg==blue,"opaque selection colours");
        check(forwarded&&calls==1,"draw arguments forwarded once");
        check(GetTextColor(dc)==white&&GetBkColor(dc)==blue&&GetBkMode(dc)==OPAQUE,"DC state restored");
        SetBkMode(dc,TRANSPARENT);
        drawReadableText(observeDraw,dc,3,5,expectedOptions,&bounds,sample,9,advances,dark,blue);
        check(observedFg==dark,"transparent text does not guess the background");
        expectedOptions=ETO_OPAQUE|ETO_CLIPPED;
        drawResult=FALSE;
        check(!drawReadableText(observeDraw,dc,3,5,expectedOptions,&bounds,sample,9,advances,dark,blue),"draw failure forwarded");
        check(observedFg==white&&forwarded,"ETO_OPAQUE selection corrected in transparent mode");
        check(GetTextColor(dc)==white&&GetBkColor(dc)==blue&&GetBkMode(dc)==TRANSPARENT,"DC restored after draw failure");
        DeleteDC(dc);
    }
    if(failures)return 1;
    printf("PASS: selected-text regression, %d highlight pairs, muted/transparent text, draw forwarding and DC restoration.\n",pairs);
    return 0;
}
