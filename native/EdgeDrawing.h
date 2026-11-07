#ifndef DARK1C_EDGE_DRAWING_H
#define DARK1C_EDGE_DRAWING_H

#include <windows.h>

typedef BOOL (WINAPI *EdgeDraw)(HDC,LPRECT,UINT,UINT);
typedef int (WINAPI *EdgeFill)(HDC,const RECT*,HBRUSH);
static void paintEdgeRect(EdgeFill fill,HDC dc,const RECT*r,COLORREF color) {
    HBRUSH brush=CreateSolidBrush(color);
    if(brush){fill(dc,r,brush);DeleteObject(brush);}
}
static BOOL drawThemedEdge(EdgeDraw draw,EdgeFill fill,HDC dc,LPRECT r,UINT edge,UINT flags,int dark) {
    if(!dark||!r||(flags&BF_DIAGONAL))return draw(dc,r,edge,flags);
    RECT original=*r;
    int saved=SaveDC(dc);if(!saved)return draw(dc,r,edge,flags);
    BOOL result=draw(dc,r,edge,flags);
    if(result){
        int n=((edge&BDR_INNER)!=0)+((edge&BDR_OUTER)!=0);
        RECT strip;COLORREF c=RGB(68,68,68);
        if(flags&BF_LEFT){strip=original;strip.right=strip.left+n;paintEdgeRect(fill,dc,&strip,c);}
        if(flags&BF_RIGHT){strip=original;strip.left=strip.right-n;paintEdgeRect(fill,dc,&strip,c);}
        if(flags&BF_TOP){strip=original;strip.bottom=strip.top+n;paintEdgeRect(fill,dc,&strip,c);}
        if(flags&BF_BOTTOM){strip=original;strip.top=strip.bottom-n;paintEdgeRect(fill,dc,&strip,c);}
        if(flags&BF_MIDDLE){
            /* Only drawn sides consume interior space. Insets on absent sides
               leave the native light fill visible as horizontal/vertical seams. */
            strip=original;
            if(flags&BF_LEFT)strip.left+=n;
            if(flags&BF_RIGHT)strip.right-=n;
            if(flags&BF_TOP)strip.top+=n;
            if(flags&BF_BOTTOM)strip.bottom-=n;
            paintEdgeRect(fill,dc,&strip,RGB(48,48,48));
        }
    }
    RestoreDC(dc,saved);return result;
}

#endif
