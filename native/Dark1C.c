#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <math.h>
#include <wchar.h>
#include <uxtheme.h>
#include <vssym32.h>
#include "MinHook.h"
#include "TextContrast.h"
#include "EdgeDrawing.h"

/* Own implementation. The DLL remains resident until 1C exits: OFF is a
   transparent pass-through, not an unsafe FreeLibrary of active hook code. */
typedef struct { volatile LONG magic,ready,enabled,hooks,error,wallpaper,darkness,request,revision,theme,appliedTheme; } State;
static State *state;
static HMODULE self;
static volatile LONG enabled;
static int installed;
static __thread int colorDepth;
typedef void cairo_t;
typedef void cairo_pattern_t;
typedef void cairo_surface_t;
typedef struct { double xx,yx,xy,yy,x0,y0; } Matrix;
typedef union { struct {int type,length;} header; struct {double x,y;} point; } PathData;
typedef struct { int status; PathData *data; int count; } Path;
static void (__cdecl *rgb)(cairo_t*,double,double,double);
static void (__cdecl *rgba)(cairo_t*,double,double,double,double);
static cairo_pattern_t* (__cdecl *prgb)(double,double,double);
static cairo_pattern_t* (__cdecl *prgba)(double,double,double,double);
static void (__cdecl *stoprgb)(cairo_pattern_t*,double,double,double,double);
static void (__cdecl *stoprgba)(cairo_pattern_t*,double,double,double,double,double);
static void (__cdecl *fill)(cairo_t*);
static void (__cdecl *save)(cairo_t*);
static void (__cdecl *restore)(cairo_t*);
static void (__cdecl *clipPreserve)(cairo_t*);
static void (__cdecl *paintAlpha)(cairo_t*,double);
static cairo_pattern_t* (__cdecl *getSource)(cairo_t*);
static int (__cdecl *getRGBA)(cairo_pattern_t*,double*,double*,double*,double*);
static void (__cdecl *extents)(cairo_t*,double*,double*,double*,double*);
static Path* (__cdecl *copyPath)(cairo_t*);
static void (__cdecl *destroyPath)(Path*);
static cairo_surface_t* (__cdecl *loadPng)(const char*);
static int (__cdecl *surfaceStatus)(cairo_surface_t*);
static void (__cdecl *destroySurface)(cairo_surface_t*);
static cairo_pattern_t* (__cdecl *surfacePattern)(cairo_surface_t*);
static void (__cdecl *patternMatrix)(cairo_pattern_t*,const Matrix*);
static void (__cdecl *patternExtend)(cairo_pattern_t*,int);
static void (__cdecl *setSource)(cairo_t*,cairo_pattern_t*);
static void (__cdecl *destroyPattern)(cairo_pattern_t*);
static cairo_surface_t *background;
static int (__cdecl *imageWidth)(cairo_surface_t*);
static int (__cdecl *imageHeight)(cairo_surface_t*);
static BOOL (WINAPI *textOut)(HDC,int,int,UINT,const RECT*,LPCWSTR,UINT,const INT*);
static int (WINAPI *fillRect)(HDC,const RECT*,HBRUSH);
static BOOL (WINAPI *drawEdge)(HDC,LPRECT,UINT,UINT);
static BOOL (WINAPI *drawFrame)(HDC,LPRECT,UINT,UINT);
static int (WINAPI *frameRect)(HDC,const RECT*,HBRUSH);
static HTHEME (WINAPI *openTheme)(HWND,LPCWSTR);
static HRESULT (WINAPI *closeTheme)(HTHEME);
static HRESULT (WINAPI *themeBackground)(HTHEME,HDC,int,int,const RECT*,const RECT*);
static SRWLOCK themeLock=SRWLOCK_INIT;
static HTHEME scrollThemes[128];
static void rememberTheme(HTHEME h) {
    if(!h)return;AcquireSRWLockExclusive(&themeLock);
    for(int i=0;i<128;i++)if(scrollThemes[i]==h){ReleaseSRWLockExclusive(&themeLock);return;}
    for(int i=0;i<128;i++)if(!scrollThemes[i]){scrollThemes[i]=h;break;}
    ReleaseSRWLockExclusive(&themeLock);
}
static int isScrollTheme(HTHEME h) {
    int found=0;AcquireSRWLockShared(&themeLock);
    for(int i=0;i<128;i++)if(scrollThemes[i]==h){found=1;break;}
    ReleaseSRWLockShared(&themeLock);return found;
}
static void paintRect(HDC dc,const RECT*r,COLORREF color) {
    HBRUSH brush=CreateSolidBrush(color);fillRect(dc,r,brush);DeleteObject(brush);
}
static int darkMode(void){return InterlockedCompareExchange(&enabled,0,0)==1;}
static int screenDC(HDC dc){return GetDeviceCaps(dc,TECHNOLOGY)==DT_RASDISPLAY;}
static void scrollArrow(HDC dc,const RECT*r,int direction,int disabled) {
    int saved=SaveDC(dc);if(!saved)return;
    int x=(r->left+r->right)/2,y=(r->top+r->bottom)/2;
    int size=(r->right-r->left<r->bottom-r->top?r->right-r->left:r->bottom-r->top)/5;if(size<2)size=2;
    POINT p[3];
    if(direction==0){p[0]=(POINT){x-size,y+size/2};p[1]=(POINT){x,y-size/2};p[2]=(POINT){x+size,y+size/2};}
    else if(direction==1){p[0]=(POINT){x-size,y-size/2};p[1]=(POINT){x,y+size/2};p[2]=(POINT){x+size,y-size/2};}
    else if(direction==2){p[0]=(POINT){x+size/2,y-size};p[1]=(POINT){x-size/2,y};p[2]=(POINT){x+size/2,y+size};}
    else{p[0]=(POINT){x-size/2,y-size};p[1]=(POINT){x+size/2,y};p[2]=(POINT){x-size/2,y+size};}
    HPEN pen=CreatePen(PS_SOLID,1,disabled?RGB(95,95,95):RGB(190,190,190));SelectObject(dc,pen);Polyline(dc,p,3);
    RestoreDC(dc,saved);DeleteObject(pen);
}
static BOOL WINAPI h_drawEdge(HDC dc,LPRECT r,UINT edge,UINT flags) {
    return drawThemedEdge(drawEdge,fillRect,dc,r,edge,flags,darkMode()&&screenDC(dc));
}
static int WINAPI h_frameRect(HDC dc,const RECT*r,HBRUSH brush) {
    if(!darkMode()||!screenDC(dc))return frameRect(dc,r,brush);
    LOGBRUSH b;COLORREF color;
    if((ULONG_PTR)brush<=COLOR_MENUBAR+1)color=GetSysColor((int)(ULONG_PTR)brush-1);
    else if(GetObjectW(brush,sizeof(b),&b)&&b.lbStyle==BS_SOLID)color=b.lbColor;else return frameRect(dc,r,brush);
    int hi=GetRValue(color),lo=hi;if(GetGValue(color)>hi)hi=GetGValue(color);if(GetBValue(color)>hi)hi=GetBValue(color);
    if(GetGValue(color)<lo)lo=GetGValue(color);if(GetBValue(color)<lo)lo=GetBValue(color);
    if(hi<180||hi-lo>40)return frameRect(dc,r,brush);
    HBRUSH substitute=CreateSolidBrush(RGB(68,68,68));int result=frameRect(dc,r,substitute);DeleteObject(substitute);return result;
}
static BOOL WINAPI h_drawFrame(HDC dc,LPRECT r,UINT type,UINT flags) {
    if(!darkMode()||!screenDC(dc)||type!=DFC_SCROLL||(flags&0x1f)>3)return drawFrame(dc,r,type,flags);
    int saved=SaveDC(dc);if(!saved)return drawFrame(dc,r,type,flags);
    paintRect(dc,r,(flags&DFCS_PUSHED)?RGB(64,64,64):RGB(37,37,37));
    scrollArrow(dc,r,flags&3,(flags&DFCS_INACTIVE)!=0);RestoreDC(dc,saved);
    if(flags&DFCS_ADJUSTRECT)InflateRect(r,-GetSystemMetrics(SM_CXEDGE),-GetSystemMetrics(SM_CYEDGE));
    return TRUE;
}
static HTHEME WINAPI h_openTheme(HWND w,LPCWSTR classes){
    HTHEME h=openTheme(w,classes);
    /* Match class tokens, not part numbers that other controls also use. */
    if(classes){const WCHAR *p=classes;while(*p){const WCHAR *e=wcschr(p,L';');size_t n=e?(size_t)(e-p):wcslen(p);
        if(n==9&&_wcsnicmp(p,L"SCROLLBAR",9)==0){rememberTheme(h);break;}if(!e)break;p=e+1;}}
    return h;
}
static HRESULT WINAPI h_closeTheme(HTHEME h){
    AcquireSRWLockExclusive(&themeLock);for(int i=0;i<128;i++)if(scrollThemes[i]==h)scrollThemes[i]=NULL;ReleaseSRWLockExclusive(&themeLock);
    return closeTheme(h);
}
static HRESULT WINAPI h_themeBackground(HTHEME h,HDC dc,int part,int s,const RECT*r,const RECT*clip){
    if(!darkMode()||!screenDC(dc)||!r||!isScrollTheme(h)||part<SBP_ARROWBTN||part>SBP_SIZEBOX)return themeBackground(h,dc,part,s,r,clip);
    int saved=SaveDC(dc);if(!saved)return themeBackground(h,dc,part,s,r,clip);
    if(clip)IntersectClipRect(dc,clip->left,clip->top,clip->right,clip->bottom);
    paintRect(dc,r,RGB(37,37,37));
    if(part==SBP_THUMBBTNHORZ||part==SBP_THUMBBTNVERT){
        RECT thumb=*r;InflateRect(&thumb,-2,-2);
        int v=s==SCRBS_PRESSED?132:s==SCRBS_HOT||s==SCRBS_HOVER?112:s==SCRBS_DISABLED?62:88;
        HBRUSH brush=CreateSolidBrush(RGB(v,v,v));SelectObject(dc,brush);SelectObject(dc,GetStockObject(NULL_PEN));
        /* Corner radius <=10 logical px, limited by the narrow thumb dimension. */
        int diameter=20;if(thumb.right-thumb.left<diameter)diameter=thumb.right-thumb.left;if(thumb.bottom-thumb.top<diameter)diameter=thumb.bottom-thumb.top;
        if(diameter>0)RoundRect(dc,thumb.left,thumb.top,thumb.right,thumb.bottom,diameter,diameter);
        RestoreDC(dc,saved);DeleteObject(brush);return S_OK;
    }
    if(part==SBP_ARROWBTN){int dir=(s-1)/4,disabled=(s-1)%4==3;if(dir>=0&&dir<=3)scrollArrow(dc,r,dir,disabled);}
    RestoreDC(dc,saved);return S_OK;
}

static int gray(int v) {
    if(InterlockedCompareExchange(&enabled,0,0)==2) {
        if(v>=245)return 250;
        if(v>=225)return 242;
        if(v>=200)return 230;
        return 32+v*140/200;
    }
    if(v>=245)return 30;
    if(v>=225)return 37;
    if(v>=200)return 48;
    if(v<=80) return 218-v/4;
    return 198-(v-80)*150/120;
}
static COLORREF remap(COLORREF c) {
    int r=GetRValue(c),g=GetGValue(c),b=GetBValue(c);
    int hi=r>g?r:g; if(b>hi)hi=b;
    int lo=r<g?r:g; if(b<lo)lo=b;
    if(hi-lo>18&&!(lo>=170&&hi-lo<=70)){
        /* Lift dark syntax colours while retaining their hue. */
        if(InterlockedCompareExchange(&enabled,0,0)!=2&&(r*2126+g*7152+b*722)/10000<110)
            return RGB(100+r*155/255,100+g*155/255,100+b*155/255);
        return c;
    }
    int v=gray((r+g+b)/3); return RGB(v,v,v);
}
static void map(double *r,double *g,double *b) {
    if(!InterlockedCompareExchange(&enabled,0,0))return;
    COLORREF original=RGB((int)(*r*255+.5),(int)(*g*255+.5),(int)(*b*255+.5));
    COLORREF c=remap(original);
    if(c==original)return;
    *r=GetRValue(c)/255.;*g=GetGValue(c)/255.;*b=GetBValue(c)/255.;
}
static void __cdecl h_rgb(cairo_t*c,double r,double g,double b){if(!colorDepth)map(&r,&g,&b);colorDepth++;rgb(c,r,g,b);colorDepth--;}
static void __cdecl h_rgba(cairo_t*c,double r,double g,double b,double a){if(!colorDepth)map(&r,&g,&b);colorDepth++;rgba(c,r,g,b,a);colorDepth--;}
static cairo_pattern_t* __cdecl h_prgb(double r,double g,double b){if(!colorDepth)map(&r,&g,&b);colorDepth++;cairo_pattern_t*p=prgb(r,g,b);colorDepth--;return p;}
static cairo_pattern_t* __cdecl h_prgba(double r,double g,double b,double a){if(!colorDepth)map(&r,&g,&b);colorDepth++;cairo_pattern_t*p=prgba(r,g,b,a);colorDepth--;return p;}
static void __cdecl h_stoprgb(cairo_pattern_t*p,double o,double r,double g,double b){if(!colorDepth)map(&r,&g,&b);colorDepth++;stoprgb(p,o,r,g,b);colorDepth--;}
static void __cdecl h_stoprgba(cairo_pattern_t*p,double o,double r,double g,double b,double a){if(!colorDepth)map(&r,&g,&b);colorDepth++;stoprgba(p,o,r,g,b,a);colorDepth--;}
static BOOL WINAPI h_textOut(HDC dc,int x,int y,UINT opts,const RECT*rc,LPCWSTR text,UINT n,const INT*dx) {
    if(!WindowFromDC(dc)||!InterlockedCompareExchange(&enabled,0,0))return textOut(dc,x,y,opts,rc,text,n,dx);
    COLORREF fg=GetTextColor(dc), bg=GetBkColor(dc);
    return drawReadableText(textOut,dc,x,y,opts,rc,text,n,dx,remap(fg),remap(bg));
}
static int WINAPI h_fillRect(HDC dc,const RECT*rc,HBRUSH brush) {
    if(!InterlockedCompareExchange(&enabled,0,0)||!WindowFromDC(dc)||colorDepth)return fillRect(dc,rc,brush);
    LOGBRUSH info;HBRUSH temp=NULL;
    if((ULONG_PTR)brush<=COLOR_MENUBAR+1){COLORREF c=GetSysColor((int)(ULONG_PTR)brush-1);temp=CreateSolidBrush(remap(c));}
    else if(GetObjectW(brush,sizeof(info),&info)&&info.lbStyle==BS_SOLID){COLORREF c=remap(info.lbColor);if(c!=info.lbColor)temp=CreateSolidBrush(c);}
    int result=fillRect(dc,rc,temp?temp:brush);if(temp)DeleteObject(temp);return result;
}
static void __cdecl h_fill(cairo_t*c) {
    /* Wallpaper is explicitly experimental: large rectangular Cairo backgrounds,
       not a semantic guarantee that the rectangle belongs to the module editor. */
    if(!InterlockedCompareExchange(&enabled,0,0)||!state||!state->wallpaper||!background){fill(c);return;}
    double r,g,b,a,x1,y1,x2,y2;
    if(getRGBA(getSource(c),&r,&g,&b,&a)!=0||a<.99||fabs(r-g)>.01||fabs(r-b)>.01||
       (InterlockedCompareExchange(&enabled,0,0)==2 ? r<.89 : (r<.10||r>.20))){fill(c);return;}
    Path *p=copyPath(c);
    int rectangular=p&&p->status==0&&p->count==11&&p->data[0].header.type==0&&p->data[2].header.type==1&&p->data[4].header.type==1&&p->data[6].header.type==1&&p->data[8].header.type==3;
    if(p)destroyPath(p);
    if(!rectangular){fill(c);return;}
    extents(c,&x1,&y1,&x2,&y2);
    if(x2-x1<400||y2-y1<180){fill(c);return;}
    save(c);clipPreserve(c);
    fill(c);
    cairo_pattern_t *pat=surfacePattern(background);
    double sx=(double)imageWidth(background)/(x2-x1),sy=(double)imageHeight(background)/(y2-y1);
    double scale=sx<sy?sx:sy;
    Matrix m={scale,0,0,scale,-x1*scale,-y1*scale};
    patternMatrix(pat,&m);patternExtend(pat,3);setSource(c,pat);
    int d=state->darkness;if(d<0)d=0;if(d>100)d=100;
    /* A dark image must not turn the light palette into a dark canvas. */
    double opacity=(100-d)/100.;
    if(InterlockedCompareExchange(&enabled,0,0)==2)opacity*=.15;
    paintAlpha(c,opacity);destroyPattern(pat);restore(c);
}
static int hook(HMODULE m,const char*n,void*h,void**original) {
    void *p=(void*)GetProcAddress(m,n);if(!p){state->error=1002;return 0;}
    MH_STATUS s=MH_CreateHook(p,h,original);if(s!=MH_OK){state->error=s;return 0;}installed++;return 1;
}
#define RESOLVE(var,name) var=(void*)GetProcAddress(cairo,name)
static DWORD WINAPI worker(void*arg) {
    wchar_t name[96];wsprintfW(name,L"Local\\Dark1C.State.v2.%lu",GetCurrentProcessId());
    HANDLE mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,NULL,PAGE_READWRITE,0,sizeof(State),name);
    if(!mapping)return 1;state=MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,sizeof(State));if(!state)return 2;
    ZeroMemory(state,sizeof(State));state->magic=0x44314331;state->revision=2;state->darkness=65;
    MH_STATUS status=MH_Initialize();if(status!=MH_OK){state->error=status;state->ready=-1;return 3;}
    HMODULE cairo=NULL;
    for(int i=0;i<100&&!cairo;i++){cairo=GetModuleHandleW(L"cairo.dll");if(!cairo)cairo=GetModuleHandleW(L"cairo-2.dll");if(!cairo)Sleep(100);}
    if(!cairo){state->error=1001;state->ready=-1;return 4;}
    hook(cairo,"cairo_set_source_rgb",h_rgb,(void**)&rgb);
    hook(cairo,"cairo_set_source_rgba",h_rgba,(void**)&rgba);
    hook(cairo,"cairo_pattern_create_rgb",h_prgb,(void**)&prgb);
    hook(cairo,"cairo_pattern_create_rgba",h_prgba,(void**)&prgba);
    hook(cairo,"cairo_pattern_add_color_stop_rgb",h_stoprgb,(void**)&stoprgb);
    hook(cairo,"cairo_pattern_add_color_stop_rgba",h_stoprgba,(void**)&stoprgba);
    RESOLVE(save,"cairo_save");RESOLVE(restore,"cairo_restore");RESOLVE(clipPreserve,"cairo_clip_preserve");
    RESOLVE(paintAlpha,"cairo_paint_with_alpha");RESOLVE(getSource,"cairo_get_source");RESOLVE(getRGBA,"cairo_pattern_get_rgba");
    RESOLVE(extents,"cairo_path_extents");RESOLVE(copyPath,"cairo_copy_path");RESOLVE(destroyPath,"cairo_path_destroy");
    RESOLVE(loadPng,"cairo_image_surface_create_from_png");RESOLVE(surfaceStatus,"cairo_surface_status");RESOLVE(destroySurface,"cairo_surface_destroy");
    RESOLVE(surfacePattern,"cairo_pattern_create_for_surface");RESOLVE(patternMatrix,"cairo_pattern_set_matrix");RESOLVE(patternExtend,"cairo_pattern_set_extend");
    RESOLVE(setSource,"cairo_set_source");RESOLVE(destroyPattern,"cairo_pattern_destroy");RESOLVE(imageWidth,"cairo_image_surface_get_width");RESOLVE(imageHeight,"cairo_image_surface_get_height");
    if(save&&restore&&clipPreserve&&paintAlpha&&getSource&&getRGBA&&extents&&copyPath&&destroyPath&&loadPng&&surfaceStatus&&destroySurface&&surfacePattern&&patternMatrix&&patternExtend&&setSource&&destroyPattern&&imageWidth&&imageHeight){
        wchar_t path[32768];char utf8[98304];DWORD length=GetModuleFileNameW(self,path,32768);
        wchar_t *end=length&&length<32768?wcsrchr(path,L'\\'):NULL;
        if(end&&(end-path)+16<32768){wcscpy(end+1,L"background.png");
            if(WideCharToMultiByte(CP_UTF8,0,path,-1,utf8,sizeof(utf8),NULL,NULL))background=loadPng(utf8);
        }
        if(background&&surfaceStatus(background)){destroySurface(background);background=NULL;}
        if(background)hook(cairo,"cairo_fill",h_fill,(void**)&fill);
    }
    HMODULE user=GetModuleHandleW(L"user32.dll"),gdi=GetModuleHandleW(L"gdi32.dll");
    hook(user,"FillRect",h_fillRect,(void**)&fillRect);
    hook(gdi,"ExtTextOutW",h_textOut,(void**)&textOut);
    hook(user,"DrawEdge",h_drawEdge,(void**)&drawEdge);
    hook(user,"DrawFrameControl",h_drawFrame,(void**)&drawFrame);
    hook(user,"FrameRect",h_frameRect,(void**)&frameRect);
    HMODULE ux=LoadLibraryExW(L"uxtheme.dll",NULL,LOAD_LIBRARY_SEARCH_SYSTEM32);
    if(ux){
        hook(ux,"OpenThemeData",h_openTheme,(void**)&openTheme);
        hook(ux,"CloseThemeData",h_closeTheme,(void**)&closeTheme);
        hook(ux,"DrawThemeBackground",h_themeBackground,(void**)&themeBackground);
    }
    if(state->error||!rgb||!rgba){state->ready=-1;return 5;}
    /* MinHook enlists/suspends target threads when applying queued hooks. */
    status=MH_QueueEnableHook(MH_ALL_HOOKS);if(status==MH_OK)status=MH_ApplyQueued();
    if(status!=MH_OK){state->error=status;state->ready=-1;return 6;}
    if(openTheme){HTHEME existing=openTheme(NULL,L"SCROLLBAR");rememberTheme(existing);/* Held until process exit. */}
    state->hooks=installed;state->ready=1;
    for(;;){
        LONG request=InterlockedCompareExchange(&state->request,0,0);
        LONG theme=InterlockedCompareExchange(&state->theme,0,0)==1?1:0;
        InterlockedExchange(&enabled,request==1?theme+1:0);
        state->appliedTheme=theme;state->enabled=enabled!=0;Sleep(80);
    }
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved) {
    if(reason==DLL_PROCESS_ATTACH){self=instance;DisableThreadLibraryCalls(instance);HANDLE t=CreateThread(NULL,0,worker,NULL,0,NULL);if(t)CloseHandle(t);else return FALSE;}
    return TRUE;
}
