#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <math.h>
#include <wchar.h>
#include "MinHook.h"

/* Own implementation. The DLL remains resident until 1C exits: OFF is a
   transparent pass-through, not an unsafe FreeLibrary of active hook code. */
typedef struct { volatile LONG magic,ready,enabled,hooks,error,wallpaper,darkness,request,revision; } State;
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

static int gray(int v) {
    if(v>=200) return 30+(255-v)*18/55;
    if(v<=80) return 218-v/4;
    return 198-(v-80)*150/120;
}
static COLORREF remap(COLORREF c) {
    int r=GetRValue(c),g=GetGValue(c),b=GetBValue(c);
    int hi=r>g?r:g; if(b>hi)hi=b;
    int lo=r<g?r:g; if(b<lo)lo=b;
    if(hi-lo>18&&!(lo>=170&&hi-lo<=70)){
        /* Lift dark syntax colours while retaining their hue. */
        if((r*2126+g*7152+b*722)/10000<110)
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
    SetTextColor(dc,remap(fg));SetBkColor(dc,remap(bg));
    BOOL ok=textOut(dc,x,y,opts,rc,text,n,dx);
    SetTextColor(dc,fg);SetBkColor(dc,bg);return ok;
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
    if(getRGBA(getSource(c),&r,&g,&b,&a)!=0||a<.99||fabs(r-g)>.01||fabs(r-b)>.01||r<.10||r>.20){fill(c);return;}
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
    paintAlpha(c,(100-d)/100.);destroyPattern(pat);restore(c);
}
static int hook(HMODULE m,const char*n,void*h,void**original) {
    void *p=(void*)GetProcAddress(m,n);if(!p)return 0;
    MH_STATUS s=MH_CreateHook(p,h,original);if(s!=MH_OK){state->error=s;return 0;}installed++;return 1;
}
#define RESOLVE(var,name) var=(void*)GetProcAddress(cairo,name)
static DWORD WINAPI worker(void*arg) {
    wchar_t name[96];wsprintfW(name,L"Local\\Dark1C.State.%lu",GetCurrentProcessId());
    HANDLE mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,NULL,PAGE_READWRITE,0,sizeof(State),name);
    if(!mapping)return 1;state=MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,sizeof(State));if(!state)return 2;
    ZeroMemory(state,sizeof(State));state->magic=0x44314331;state->revision=1;state->darkness=65;
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
        wchar_t path[MAX_PATH];char utf8[2048];GetModuleFileNameW(self,path,MAX_PATH);wchar_t *end=wcsrchr(path,L'\\');if(end)wcscpy(end+1,L"background.png");
        WideCharToMultiByte(CP_UTF8,0,path,-1,utf8,sizeof(utf8),NULL,NULL);
        background=loadPng(utf8);if(surfaceStatus(background)){destroySurface(background);background=NULL;}
        if(background)hook(cairo,"cairo_fill",h_fill,(void**)&fill);
    }
    HMODULE user=GetModuleHandleW(L"user32.dll"),gdi=GetModuleHandleW(L"gdi32.dll");
    hook(user,"FillRect",h_fillRect,(void**)&fillRect);
    hook(gdi,"ExtTextOutW",h_textOut,(void**)&textOut);
    if(state->error||!rgb||!rgba){state->ready=-1;return 5;}
    /* MinHook enlists/suspends target threads when applying queued hooks. */
    status=MH_QueueEnableHook(MH_ALL_HOOKS);if(status==MH_OK)status=MH_ApplyQueued();
    if(status!=MH_OK){state->error=status;state->ready=-1;return 6;}
    state->hooks=installed;state->ready=1;
    for(;;){LONG request=InterlockedCompareExchange(&state->request,0,0);InterlockedExchange(&enabled,request==1);state->enabled=enabled;Sleep(80);}
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved) {
    if(reason==DLL_PROCESS_ATTACH){self=instance;DisableThreadLibraryCalls(instance);HANDLE t=CreateThread(NULL,0,worker,NULL,0,NULL);if(t)CloseHandle(t);else return FALSE;}
    return TRUE;
}
