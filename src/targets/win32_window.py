from sys import platform

if platform.lower() == "win32":
    import ctypes
    from ctypes import wintypes
    import psutil

    user32 = ctypes.windll.user32
    kernel32 = ctypes.windll.kernel32

    EnumWindows = user32.EnumWindows
    EnumWindowsProc = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
    GetWindowTextW = user32.GetWindowTextW
    GetWindowTextLengthW = user32.GetWindowTextLengthW
    IsWindowVisible = user32.IsWindowVisible
    GetWindowThreadProcessId = user32.GetWindowThreadProcessId
    GetWindowRect = user32.GetWindowRect
    ScreenToClient = user32.ScreenToClient
    PostMessageW = user32.PostMessageW
    MapVirtualKeyW = user32.MapVirtualKeyW
    VkKeyScanW = user32.VkKeyScanW

    WM_MOUSEMOVE = 0x0200
    WM_LBUTTONDOWN = 0x0201
    WM_LBUTTONUP = 0x0202
    WM_RBUTTONDOWN = 0x0204
    WM_RBUTTONUP = 0x0205
    WM_MBUTTONDOWN = 0x0207
    WM_MBUTTONUP = 0x0208
    WM_MOUSEWHEEL = 0x020A
    WM_KEYDOWN = 0x0100
    WM_KEYUP = 0x0101

    MK_LBUTTON = 0x0001
    MK_RBUTTON = 0x0002
    MK_MBUTTON = 0x0010

    class RECT(ctypes.Structure):
        _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long), ("right", ctypes.c_long), ("bottom", ctypes.c_long)]

    class POINT(ctypes.Structure):
        _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]

    def MAKELPARAM(low, high):
        return ctypes.c_long((high << 16) | (low & 0xFFFF)).value

    def LOWORD(dword):
        return dword & 0xFFFF

    def HIWORD(dword):
        return (dword >> 16) & 0xFFFF

    def list_windows():
        results = []

        def callback(hwnd, lparam):
            if not IsWindowVisible(hwnd):
                return True
            length = GetWindowTextLengthW(hwnd)
            if length == 0:
                return True
            title = ctypes.create_unicode_buffer(length + 1)
            GetWindowTextW(hwnd, title, length + 1)
            if not title.value.strip():
                return True
            pid = wintypes.DWORD()
            GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            exe = None
            try:
                exe = psutil.Process(pid.value).name()
            except Exception:
                exe = None
            results.append({
                "hwnd": int(hwnd),
                "title": title.value,
                "pid": int(pid.value),
                "exe": exe or ""
            })
            return True

        EnumWindows(EnumWindowsProc(callback), 0)
        return results

    class Win32WindowDispatcher:
        def __init__(self, config, macro):
            self.macro = macro
            self.hwnd = wintypes.HWND(config.get("hwnd"))

        def _client_coords(self, screen_x, screen_y):
            # Convert screen coords to client coords for target window
            pt = POINT(screen_x, screen_y)
            # Copy point since ScreenToClient mutates in place
            pt_conv = POINT(pt.x, pt.y)
            ScreenToClient(self.hwnd, ctypes.byref(pt_conv))
            return pt_conv.x, pt_conv.y

        def dispatch_mouse_move(self, x, y, macro_events):
            cx, cy = self._client_coords(x, y)
            PostMessageW(self.hwnd, WM_MOUSEMOVE, 0, MAKELPARAM(cx, cy))

        def dispatch_mouse_click(self, button_name, pressed, x, y, macro_events):
            cx, cy = self._client_coords(x, y)
            btn_map_down = {
                'left': WM_LBUTTONDOWN,
                'right': WM_RBUTTONDOWN,
                'middle': WM_MBUTTONDOWN,
            }
            btn_map_up = {
                'left': WM_LBUTTONUP,
                'right': WM_RBUTTONUP,
                'middle': WM_MBUTTONUP,
            }
            msg = btn_map_down[button_name] if pressed else btn_map_up[button_name]
            PostMessageW(self.hwnd, msg, 0, MAKELPARAM(cx, cy))

        def dispatch_scroll(self, dx, dy):
            # dy positive means scroll up; WM_MOUSEWHEEL expects delta in high word
            delta = int(-dy) * 120
            wparam = MAKELPARAM(0, (delta & 0xFFFF))
            PostMessageW(self.hwnd, WM_MOUSEWHEEL, wparam, MAKELPARAM(0, 0))

        def _to_vk(self, key):
            # Try direct integer (already VK)
            try:
                if isinstance(key, int):
                    return key
            except Exception:
                pass
            # If string single char
            try:
                if isinstance(key, str) and len(key) == 1:
                    vk = VkKeyScanW(ord(key)) & 0xFF
                    return vk
            except Exception:
                pass
            # Fallback: common keys mapping
            common = {
                'Key.enter': 0x0D,
                'Key.backspace': 0x08,
                'Key.tab': 0x09,
                'Key.esc': 0x1B,
                'Key.space': 0x20,
                'Key.shift': 0x10,
                'Key.ctrl': 0x11,
                'Key.alt': 0x12,
                'Key.left': 0x25,
                'Key.up': 0x26,
                'Key.right': 0x27,
                'Key.down': 0x28,
                'Key.delete': 0x2E,
            }
            if isinstance(key, str) and key in common:
                return common[key]
            try:
                # String like 'Key.f5'
                if isinstance(key, str) and key.startswith('Key.f'):
                    num = int(key.split('f')[1])
                    return 0x70 + (num - 1)
            except Exception:
                pass
            return 0

        def dispatch_key(self, key, pressed):
            vk = self._to_vk(key)
            if vk == 0:
                return
            msg = WM_KEYDOWN if pressed else WM_KEYUP
            PostMessageW(self.hwnd, msg, vk, 0)

        def release_all_mouse_buttons(self):
            PostMessageW(self.hwnd, WM_LBUTTONUP, 0, 0)
            PostMessageW(self.hwnd, WM_MBUTTONUP, 0, 0)
else:
    def list_windows():
        return []

    class Win32WindowDispatcher:
        def __init__(self, config, macro):
            raise RuntimeError("Win32WindowDispatcher is only available on Windows")
