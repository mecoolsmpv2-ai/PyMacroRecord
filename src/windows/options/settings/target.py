from tkinter import *
from tkinter.ttk import *
from windows.popup import Popup
from targets.chrome_cdp import list_chrome_tabs


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
        Radiobutton(mode_frame, text="Chrome Tab (CDP)", variable=mode_var, value="ChromeTab").pack(anchor=W, padx=10, pady=2)
        mode_frame.pack(fill=X, padx=10, pady=10)

        chrome_frame = LabelFrame(self, text="Chrome Tabs")
        cols = ("title", "url")
        tree = Treeview(chrome_frame, columns=cols, show='headings', height=6)
        for c in cols:
            tree.heading(c, text=c)
            tree.column(c, width=200 if c == 'title' else 230)
        tree.pack(fill=BOTH, expand=True)

        refresh_btn = Button(chrome_frame, text="Refresh", command=lambda: self._populate_tabs(tree))
        refresh_btn.pack(anchor=E, padx=5, pady=5)
        chrome_frame.pack(fill=BOTH, expand=True, padx=10, pady=10)

        self._populate_tabs(tree)

        btns = Frame(self)
        def on_confirm():
            selected = tree.focus()
            conf = {"Type": mode_var.get()}
            if mode_var.get() == "ChromeTab" and selected:
                vals = tree.item(selected, 'values')
                title, url = vals[0], vals[1]
                # find ws url
                ws_url = ""
                for tab in list_chrome_tabs():
                    if tab.get("title") == title and tab.get("url") == url:
                        ws_url = tab.get("webSocketDebuggerUrl", "")
                        break
                conf["Chrome"] = {"title": title, "webSocketDebuggerUrl": ws_url}
            self.settings.change_settings("Target", None, None, conf)
            self.destroy()

        Button(btns, text=self.main_app.text_content["global"]["confirm_button"], command=on_confirm).pack(side=LEFT, padx=8)
        Button(btns, text=self.main_app.text_content["global"]["cancel_button"], command=self.destroy).pack(side=LEFT)
        btns.pack(pady=8)
        self.wait_window()
        main_app.prevent_record = False

    def _populate_tabs(self, tree):
        for item in tree.get_children():
            tree.delete(item)
        for tab in list_chrome_tabs():
            if tab.get("type") != "page":
                continue
            tree.insert('', END, values=(tab.get("title", ""), tab.get("url", "")))
