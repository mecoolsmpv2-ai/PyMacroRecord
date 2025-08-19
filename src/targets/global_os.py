from pynput.mouse import Button, Controller as MouseController
from pynput.keyboard import Controller as KeyboardController


class GlobalOSDispatcher:
    def __init__(self, macro):
        self.macro = macro
        self.mouse = macro.mouseControl if hasattr(macro, 'mouseControl') else MouseController()
        self.keyboard = macro.keyboardControl if hasattr(macro, 'keyboardControl') else KeyboardController()

    def dispatch_mouse_move(self, x, y, macro_events):
        self.mouse.position = (x, y)

    def dispatch_mouse_click(self, button_name, pressed, x, y, macro_events):
        self.mouse.position = (x, y)
        button = {
            'left': Button.left,
            'right': Button.right,
            'middle': Button.middle,
        }[button_name]
        if pressed:
            self.mouse.press(button)
        else:
            self.mouse.release(button)

    def dispatch_scroll(self, dx, dy):
        self.mouse.scroll(dx, dy)

    def dispatch_key(self, key, pressed):
        if pressed:
            self.keyboard.press(key)
        else:
            self.keyboard.release(key)

    def release_all_mouse_buttons(self):
        try:
            self.mouse.release(Button.left)
        except Exception:
            pass
        try:
            self.mouse.release(Button.middle)
        except Exception:
            pass
