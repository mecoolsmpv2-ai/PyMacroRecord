import json
import time
import threading
import requests
from websocket import create_connection


def list_chrome_tabs():
    # Default remote debugging endpoint for Chrome
    endpoints = [
        "http://localhost:9222/json",
        "http://127.0.0.1:9222/json",
    ]
    for endpoint in endpoints:
        try:
            resp = requests.get(endpoint, timeout=0.5)
            if resp.ok:
                tabs = resp.json()
                return tabs
        except Exception:
            continue
    return []


class ChromeTabDispatcher:
    def __init__(self, chrome_target_config, macro):
        self.macro = macro
        self.tab_config = chrome_target_config or {}
        self.ws_url = self.tab_config.get("webSocketDebuggerUrl")
        if not self.ws_url:
            # attempt to find by title
            title = self.tab_config.get("title", "")
            for tab in list_chrome_tabs():
                if title and tab.get("title", "").startswith(title):
                    self.ws_url = tab.get("webSocketDebuggerUrl")
                    break
        if not self.ws_url:
            raise RuntimeError("No Chrome tab websocket URL resolved")
        self._ws = create_connection(self.ws_url)
        self._id = 0
        self._lock = threading.Lock()
        self._send_cmd({"method": "Page.enable"})
        self._send_cmd({"method": "Input.setIgnoreInputEvents", "params": {"ignore": False}})

    def _send_cmd(self, msg):
        with self._lock:
            self._id += 1
            msg_with_id = {"id": self._id, **msg}
            self._ws.send(json.dumps(msg_with_id))
            # Optionally read results, but we keep it fire-and-forget for speed
            return self._id

    def _normalize_point(self, x, y, macro_events):
        # Normalize recorded coordinates to current viewport if needed. For simplicity, send as-is.
        return x, y

    def dispatch_mouse_move(self, x, y, macro_events):
        x, y = self._normalize_point(x, y, macro_events)
        self._send_cmd({
            "method": "Input.dispatchMouseEvent",
            "params": {
                "type": "mouseMoved",
                "x": x,
                "y": y,
                "buttons": 0
            }
        })

    def dispatch_mouse_click(self, button_name, pressed, x, y, macro_events):
        x, y = self._normalize_point(x, y, macro_events)
        mapping = {"left": "left", "right": "right", "middle": "middle"}
        self._send_cmd({
            "method": "Input.dispatchMouseEvent",
            "params": {
                "type": "mousePressed" if pressed else "mouseReleased",
                "x": x,
                "y": y,
                "button": mapping.get(button_name, "left"),
                "clickCount": 1
            }
        })

    def dispatch_scroll(self, dx, dy):
        self._send_cmd({
            "method": "Input.dispatchMouseEvent",
            "params": {
                "type": "mouseWheel",
                "x": 0,
                "y": 0,
                "deltaX": dx,
                "deltaY": -dy
            }
        })

    def dispatch_key(self, key, pressed):
        # Accepts either a pynput Key or string; convert to text if possible
        text = None
        key_str = None
        try:
            if hasattr(key, 'vk'):
                key_str = getattr(key, 'char', None)
            elif isinstance(key, str):
                key_str = key
            else:
                key_str = str(key)
        except Exception:
            key_str = str(key)

        text = key_str if key_str and len(key_str) == 1 else None
        self._send_cmd({
            "method": "Input.dispatchKeyEvent",
            "params": {
                "type": "keyDown" if pressed else "keyUp",
                "text": text,
                "unmodifiedText": text,
                "key": key_str if key_str else "Unidentified"
            }
        })

    def release_all_mouse_buttons(self):
        # No-Op; Chrome tracks button state implicitly with events
        pass
