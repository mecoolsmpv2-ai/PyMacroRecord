from tkinter import *
from tkinter.ttk import *
from windows.popup import Popup
from targets.chrome_cdp import list_chrome_tabs
from targets.win32_window import list_windows


class TargetSelection(Popup):
    def __init__(self, parent, main_app):
        super().__init__("Target Selection", 450, 300, parent)
        main_app.prevent_record = True
        self.settings = main_app.settings
        self.main_app = main_app

        userSettings = self.settings.settings_dict
        target = userSettings.get("Target", {"Type": "Global"})

        mode_frame = LabelFrame(self, text="Mode")
        mode_var = StringVar(value=target.get("Type", "Global"))

        Radiobutton(mode_frame, text="Global (OS-wide)", variable=mode_var, value="Global").pack(anchor=W, padx=10, pady=2)
        Radiobutton(mode_frame, text="Windows App (HWND)", variable=mode_var, value="Win32Window").pack(anchor=W, padx=10, pady=2)
        Radiobutton(mode_frame, text="Chrome Tab (CDP)", variable=mode_var, value="ChromeTab").pack(anchor=W, padx=10, pady=2)
        mode_frame.pack(fill=X, padx=10, pady=10)

        chrome_frame = LabelFrame(self, text="Targets")
        cols = ("name", "info", "id")
        tree = Treeview(chrome_frame, columns=cols, show='headings', height=8)
        for c in cols:
            tree.heading(c, text=c)
            tree.column(c, width=220 if c == 'name' else 260 if c == 'info' else 120)
        tree.pack(fill=BOTH, expand=True)

        refresh_btn = Button(chrome_frame, text="Refresh", command=lambda: self._populate_targets(tree, mode_var.get()))
        refresh_btn.pack(anchor=E, padx=5, pady=5)
        chrome_frame.pack(fill=BOTH, expand=True, padx=10, pady=10)

        self._populate_targets(tree, mode_var.get())

        def on_mode_change(*args):
            self._populate_targets(tree, mode_var.get())
        mode_var.trace_add('write', lambda *args: on_mode_change())

        btns = Frame(self)
        def on_confirm():
            selected = tree.focus()
            conf = {"Type": mode_var.get()}
            if mode_var.get() == "ChromeTab" and selected:
                vals = tree.item(selected, 'values')
                name, info, ws = vals[0], vals[1], vals[2]
                conf["Chrome"] = {"title": name, "webSocketDebuggerUrl": ws}
            elif mode_var.get() == "Win32Window" and selected:
                vals = tree.item(selected, 'values')
                name, info, hwnd = vals[0], vals[1], vals[2]
                conf["Win32"] = {"title": name, "hwnd": int(hwnd)}
            self.settings.change_settings("Target", None, None, conf)
            self.destroy()

        Button(btns, text=self.main_app.text_content["global"]["confirm_button"], command=on_confirm).pack(side=LEFT, padx=8)
        Button(btns, text=self.main_app.text_content["global"]["cancel_button"], command=self.destroy).pack(side=LEFT)
        btns.pack(pady=8)
        self.wait_window()
        main_app.prevent_record = False

    def _populate_targets(self, tree, mode):
        for item in tree.get_children():
            tree.delete(item)
        if mode == "ChromeTab":
            for tab in list_chrome_tabs():
                if tab.get("type") != "page":
                    continue
                name = tab.get("title", "")
                info = tab.get("url", "")
                ws = tab.get("webSocketDebuggerUrl", "")
                tree.insert('', END, values=(name, info, ws))
        elif mode == "Win32Window":
            for win in list_windows():
                name = win.get("title", "")
                info = f"{win.get('exe','')} (PID {win.get('pid','')})"
                hwnd = win.get("hwnd", "")
                tree.insert('', END, values=(name, info, hwnd))
