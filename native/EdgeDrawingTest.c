#define WIN32_LEAN_AND_MEAN
#include <stdio.h>
#include <string.h>
#include "EdgeDrawing.h"

enum { WIDTH=40,HEIGHT=32 };
typedef struct {HDC dc;HBITMAP bitmap;HGDIOBJ old;DWORD *pixels;} Canvas;
static int failures,cases;
static const COLORREF untouched=RGB(173,81,139);
static DWORD dibColor(COLORREF c){return (GetRValue(c)<<16)|(GetGValue(c)<<8)|GetBValue(c);}
static void check(int condition,const char*message) {
    if(!condition){if(failures<12)fprintf(stderr,"FAIL: %s\n",message);failures++;}
}
static Canvas canvas(void) {
    Canvas c={0};BITMAPINFO info={0};info.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);
    info.bmiHeader.biWidth=WIDTH;info.bmiHeader.biHeight=-HEIGHT;
    info.bmiHeader.biPlanes=1;info.bmiHeader.biBitCount=32;info.bmiHeader.biCompression=BI_RGB;
    c.dc=CreateCompatibleDC(NULL);
    c.bitmap=CreateDIBSection(c.dc,&info,DIB_RGB_COLORS,(void**)&c.pixels,NULL,0);
    if(c.dc&&c.bitmap)c.old=SelectObject(c.dc,c.bitmap);
    return c;
}
static void reset(Canvas*c){GdiFlush();for(int i=0;i<WIDTH*HEIGHT;i++)c->pixels[i]=dibColor(untouched);}
static void dispose(Canvas*c){if(c->old)SelectObject(c->dc,c->old);if(c->bitmap)DeleteObject(c->bitmap);if(c->dc)DeleteDC(c->dc);}
static int samePixels(Canvas*a,Canvas*b){
    GdiFlush();for(int i=0;i<WIDTH*HEIGHT;i++)if((a->pixels[i]&0xffffff)!=(b->pixels[i]&0xffffff))return 0;return 1;
}
static void compareCase(Canvas*native,Canvas*themed,UINT edge,UINT flags,int dark,int clipped) {
    const RECT bounds={5,5,35,27};RECT a=bounds,b=bounds,interior=bounds;
    reset(native);reset(themed);
    if(clipped){IntersectClipRect(native->dc,9,8,30,24);IntersectClipRect(themed->dc,9,8,30,24);}
    RECT clipBefore,clipAfter;GetClipBox(themed->dc,&clipBefore);
    COLORREF textBefore=GetTextColor(themed->dc),backgroundBefore=GetBkColor(themed->dc);
    HGDIOBJ penBefore=GetCurrentObject(themed->dc,OBJ_PEN),brushBefore=GetCurrentObject(themed->dc,OBJ_BRUSH);
    BOOL expected=DrawEdge(native->dc,&a,edge,flags);
    BOOL actual=drawThemedEdge(DrawEdge,FillRect,themed->dc,&b,edge,flags,dark);
    check(actual==expected,"return value differs from DrawEdge");
    check(EqualRect(&a,&b),"BF_ADJUST rectangle differs from DrawEdge");
    GetClipBox(themed->dc,&clipAfter);
    check(EqualRect(&clipBefore,&clipAfter)&&textBefore==GetTextColor(themed->dc)&&backgroundBefore==GetBkColor(themed->dc)&&
          penBefore==GetCurrentObject(themed->dc,OBJ_PEN)&&brushBefore==GetCurrentObject(themed->dc,OBJ_BRUSH),"caller GDI state changed");
    if(!dark||(flags&BF_DIAGONAL)||!expected)check(samePixels(native,themed),"pass-through pixels differ");
    else {
        /* Ask Windows for the actual interior; do not duplicate the implementation's inset math. */
        DrawEdge(native->dc,&interior,edge,(flags&~BF_MIDDLE)|BF_ADJUST);
        GdiFlush();int mismatch=0;
        for(int y=0;y<HEIGHT;y++)for(int x=0;x<WIDTH;x++) {
            POINT p={x,y};COLORREF expectedPixel=untouched;
            if(PtInRect(&bounds,p)&&(!clipped||(x>=9&&x<30&&y>=8&&y<24))) {
                if(!PtInRect(&interior,p))expectedPixel=RGB(68,68,68);
                else if(flags&BF_MIDDLE)expectedPixel=RGB(48,48,48);
            }
            if((themed->pixels[y*WIDTH+x]&0xffffff)!=dibColor(expectedPixel))mismatch++;
        }
        if(mismatch&&failures<12)fprintf(stderr,"edge=%u flags=%u clipped=%d: %d incorrect pixels\n",edge,flags,clipped,mismatch);
        check(mismatch==0,"edge/interior pixels or untouched region incorrect");
    }
    if(clipped){SelectClipRgn(native->dc,NULL);SelectClipRgn(themed->dc,NULL);}
    cases++;
}
int main(void) {
    Canvas native=canvas(),themed=canvas();
    if(!native.old||!themed.old){fprintf(stderr,"Cannot create private DIB canvases\n");dispose(&native);dispose(&themed);return 1;}
    const UINT edges[]={0,BDR_RAISEDOUTER,BDR_SUNKENOUTER,BDR_RAISEDINNER,BDR_SUNKENINNER,EDGE_RAISED,EDGE_SUNKEN,EDGE_ETCHED,EDGE_BUMP};
    const UINT styles[]={0,BF_FLAT,BF_MONO,BF_SOFT};
    for(unsigned e=0;e<sizeof(edges)/sizeof(edges[0]);e++)for(unsigned s=0;s<sizeof(styles)/sizeof(styles[0]);s++)
    for(UINT sides=0;sides<=BF_RECT;sides++)for(int middle=0;middle<2;middle++)for(int adjust=0;adjust<2;adjust++) {
        UINT flags=sides|styles[s]|(middle?BF_MIDDLE:0)|(adjust?BF_ADJUST:0);
        compareCase(&native,&themed,edges[e],flags,1,0);
        compareCase(&native,&themed,edges[e],flags,0,0);
    }
    compareCase(&native,&themed,EDGE_SUNKEN,BF_LEFT|BF_MIDDLE|BF_ADJUST,1,1);
    compareCase(&native,&themed,EDGE_RAISED,BF_DIAGONAL_ENDTOPRIGHT|BF_MIDDLE|BF_ADJUST,1,0);
    compareCase(&native,&themed,BDR_RAISEDOUTER|BDR_SUNKENOUTER,BF_RECT|BF_MIDDLE|BF_ADJUST,1,0);
    dispose(&native);dispose(&themed);
    if(failures){fprintf(stderr,"%d failures across %d cases\n",failures,cases);return 1;}
    printf("PASS: %d DrawEdge pixel cases, all side masks, interior fill, BF_ADJUST, clipping and pass-through.\n",cases);
    return 0;
}
