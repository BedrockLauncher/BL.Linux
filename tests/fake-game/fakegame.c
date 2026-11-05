/*
 * Stand-in for Minecraft.Windows.exe (UWP) used by the integration tests: it touches the same platform pieces the
 * real game depends on at startup and writes a world file where the game keeps its saves.
 *   1. the VC++ UWP runtime (msvcp140_app, vcruntime140_app, vcruntime140_1_app, concrt140_app)
 *   2. Windows.Storage.ApplicationData.Current.LocalFolder (via combase / WinRT activation)
 *   3. <LocalFolder>\games\com.mojang\minecraftWorlds\fake-world\level.dat
 * Output: one line per step, "OK ..." or "FAIL ..."; exit code 0 when every step passed.
 * Build: x86_64-w64-mingw32-gcc -O1 -o Minecraft.Windows.exe fakegame.c
 */
#include <windows.h>
#include <stdio.h>
#include <wchar.h>

typedef HRESULT (WINAPI *RoInitialize_t)(int);
typedef HRESULT (WINAPI *WindowsCreateString_t)(const WCHAR *, UINT32, void **);
typedef const WCHAR *(WINAPI *WindowsGetStringRawBuffer_t)(void *, UINT32 *);
typedef HRESULT (WINAPI *RoGetActivationFactory_t)(void *, const GUID *, void **);
typedef HRESULT (WINAPI *Method_t)(void *, void *);
typedef HRESULT (WINAPI *QueryInterface_t)(void *, const GUID *, void **);

static const GUID IID_IApplicationDataStatics = {0x5612147b, 0xe843, 0x45e3, {0x94, 0xd8, 0x06, 0x16, 0x9e, 0x3c, 0x8e, 0x17}};
static const GUID IID_IStorageItem = {0x4207a996, 0xca2f, 0x42f7, {0xbd, 0xe8, 0x8b, 0x10, 0x45, 0x7a, 0x7f, 0x30}};

static void *method(void *obj, int index) { return (*(void ***)obj)[index]; }

static int create_tree(WCHAR *path)
{
    for (WCHAR *p = path + 3; *p; p++)
    {
        if (*p != '\\') continue;
        *p = 0;
        CreateDirectoryW(path, NULL);
        *p = '\\';
    }
    return CreateDirectoryW(path, NULL) || GetLastError() == ERROR_ALREADY_EXISTS;
}

int main(void)
{
    int failures = 0;
    const char *dlls[] = {"msvcp140_app.dll", "vcruntime140_app.dll", "vcruntime140_1_app.dll", "concrt140_app.dll"};
    for (int i = 0; i < 4; i++)
    {
        HMODULE h = LoadLibraryA(dlls[i]);
        printf("%s load %s\n", h ? "OK" : "FAIL", dlls[i]);
        if (!h) failures++;
    }

    HMODULE combase = LoadLibraryA("combase.dll");
    RoInitialize_t RoInitialize = (RoInitialize_t)GetProcAddress(combase, "RoInitialize");
    WindowsCreateString_t WindowsCreateString = (WindowsCreateString_t)GetProcAddress(combase, "WindowsCreateString");
    WindowsGetStringRawBuffer_t WindowsGetStringRawBuffer = (WindowsGetStringRawBuffer_t)GetProcAddress(combase, "WindowsGetStringRawBuffer");
    RoGetActivationFactory_t RoGetActivationFactory = (RoGetActivationFactory_t)GetProcAddress(combase, "RoGetActivationFactory");
    if (!RoInitialize || !WindowsCreateString || !RoGetActivationFactory) { printf("FAIL combase exports\n"); return 1; }

    RoInitialize(1 /* RO_INIT_MULTITHREADED */);
    const WCHAR *name = L"Windows.Storage.ApplicationData";
    void *hname = NULL, *statics = NULL, *appdata = NULL, *folder = NULL, *item = NULL, *hpath = NULL;
    HRESULT hr = WindowsCreateString(name, (UINT32)wcslen(name), &hname);
    if (SUCCEEDED(hr)) hr = RoGetActivationFactory(hname, &IID_IApplicationDataStatics, &statics);
    if (SUCCEEDED(hr)) hr = ((Method_t)method(statics, 6))(statics, &appdata);   /* get_Current */
    if (SUCCEEDED(hr)) hr = ((Method_t)method(appdata, 12))(appdata, &folder);   /* get_LocalFolder */
    if (SUCCEEDED(hr)) hr = ((QueryInterface_t)method(folder, 0))(folder, &IID_IStorageItem, &item);
    if (SUCCEEDED(hr)) hr = ((Method_t)method(item, 12))(item, &hpath);          /* get_Path */
    if (FAILED(hr)) { printf("FAIL ApplicationData.Current.LocalFolder.Path hr=%08lx\n", hr); return 1; }

    WCHAR world[MAX_PATH];
    const WCHAR *local = WindowsGetStringRawBuffer(hpath, NULL);
    printf("OK LocalFolder %ls\n", local);
    swprintf(world, MAX_PATH, L"%ls\\games\\com.mojang\\minecraftWorlds\\fake-world", local);
    if (!create_tree(world)) { printf("FAIL mkdir %ls (%lu)\n", world, GetLastError()); return 1; }

    WCHAR level[MAX_PATH];
    swprintf(level, MAX_PATH, L"%ls\\level.dat", world);
    HANDLE file = CreateFileW(level, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    DWORD written = 0;
    BOOL ok = file != INVALID_HANDLE_VALUE && WriteFile(file, "fake level", 10, &written, NULL);
    if (file != INVALID_HANDLE_VALUE) CloseHandle(file);
    printf("%s write %ls\n", ok ? "OK" : "FAIL", level);
    if (!ok) failures++;

    return failures ? 1 : 0;
}
