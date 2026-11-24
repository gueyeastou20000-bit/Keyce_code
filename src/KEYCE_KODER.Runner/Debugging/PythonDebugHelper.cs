namespace KEYCE_KODER.Runner.Debugging;

/// <summary>
/// Moteur de débogage Python, écrit avec le module « bdb » fourni avec Python (rien à installer).
/// KEYCE KODER l'écrit dans le dossier temporaire puis le lance : python -u keyce_debugger.py port script.
/// Il dialogue avec l'IDE en JSON (une ligne par message) sur un socket local (127.0.0.1).
/// </summary>
internal static class PythonDebugHelper
{
    public const string FileName = "keyce_debugger.py";

    public const string Script = """"
# KEYCE KODER : moteur de débogage Python (module bdb, inclus dans Python).
# Lancé par l'IDE : python -u keyce_debugger.py <port> <script> [arguments...]
# Dialogue avec l'IDE en JSON (une ligne par message) sur un socket local.
import bdb
import json
import os
import queue
import reprlib
import socket
import sys
import threading
import traceback
import types

HELPER = os.path.normcase(os.path.abspath(__file__))
SIMPLE = (str, bytes, int, float, complex, bool, type(None))
NOT_EXPANDED = (types.ModuleType, types.FunctionType, types.BuiltinFunctionType, types.MethodType, type)
MAX_CHILDREN = 300

_repr = reprlib.Repr()
_repr.maxstring = 200
_repr.maxother = 200
_repr.maxlist = 20
_repr.maxtuple = 20
_repr.maxdict = 20
_repr.maxset = 20
_repr.maxlevel = 2


class Session(bdb.Bdb):
    def __init__(self, sock, script):
        super().__init__()
        self.sock = sock
        self.reader = sock.makefile("r", encoding="utf-8", newline="\n")
        self.lock = threading.Lock()
        self.commands = queue.Queue()
        self.script = self.canonic(script)
        self.root = os.path.normcase(os.path.dirname(os.path.abspath(script)))
        self.frames = []
        self.refs = {}
        self.next_ref = 1
        self.reason = None
        self.started = False

    # ---------- Communication ----------

    def send(self, message):
        data = (json.dumps(message, ensure_ascii=False) + "\n").encode("utf-8")
        with self.lock:
            try:
                self.sock.sendall(data)
            except OSError:
                pass

    def reply(self, request, body):
        self.send({"type": "response", "seq": request.get("seq", 0), "body": body})

    def read_loop(self):
        try:
            for line in self.reader:
                if not line.strip():
                    continue
                message = json.loads(line)
                command = message.get("cmd")
                if command == "setBreakpoints":
                    self.reply(message, {"lines": self.apply_breakpoints(message)})
                elif command == "pause":
                    self.reason = "pause"
                    self.set_step()
                else:
                    self.commands.put(message)
        except (OSError, ValueError):
            pass
        # L'IDE s'est déconnecté : le programme continue sans s'arrêter
        self.clear_all_breaks()
        self.commands.put({"cmd": "disconnect"})

    # ---------- Points d'arrêt ----------

    def apply_breakpoints(self, message):
        filename = self.canonic(message["file"])
        self.clear_all_file_breaks(filename)
        verified = []
        for breakpoint in message.get("breakpoints", []):
            line = int(breakpoint["line"])
            condition = breakpoint.get("condition") or None
            if not self.set_break(filename, line, cond=condition):
                verified.append(line)
        return verified

    # ---------- Traçage ----------

    def set_continue(self):
        # Contrairement à bdb, le traçage reste actif : pause et nouveaux points d'arrêt possibles
        self._set_stopinfo(self.botframe, None, -1)

    def dispatch_call(self, frame, arg):
        result = super().dispatch_call(frame, arg)
        return self.trace_dispatch if result is None and not self.quitting else result

    def is_visible(self, frame):
        filename = self.canonic(frame.f_code.co_filename)
        return not filename.startswith("<") and filename != HELPER and os.path.basename(filename) != "bdb.py"

    def is_user_code(self, frame):
        filename = self.canonic(frame.f_code.co_filename)
        if not self.is_visible(frame) or "site-packages" in filename:
            return False
        return filename == self.script or filename.startswith(self.root + os.sep)

    def user_call(self, frame, argument_list):
        pass

    def user_return(self, frame, return_value):
        pass

    def user_exception(self, frame, exc_info):
        pass

    def user_line(self, frame):
        if not self.started:
            self.started = True
            if not self.get_breaks(self.canonic(frame.f_code.co_filename), frame.f_lineno):
                self.set_continue()
                return
        if not self.is_user_code(frame):
            return
        reason = self.reason or "breakpoint"
        self.reason = None
        stack, _ = self.get_stack(frame, None)
        frames = [(f, f.f_lineno) for f, _ in reversed(stack) if self.is_visible(f)]
        self.stop(frames, reason, None)
        self.command_loop(frame, post_mortem=False)

    # ---------- Arrêt : pile, variables, commandes ----------

    def stop(self, frames, reason, description):
        self.frames = frames
        self.refs = {}
        self.next_ref = 1
        self.send({
            "type": "event",
            "event": "stopped",
            "reason": reason,
            "description": description,
            "frames": [
                {"id": i, "name": f.f_code.co_name, "file": os.path.abspath(f.f_code.co_filename), "line": line}
                for i, (f, line) in enumerate(frames)
            ],
        })

    def command_loop(self, frame, post_mortem):
        while True:
            message = self.commands.get()
            command = message.get("cmd")
            if command in ("continue", "next", "stepIn", "stepOut", "disconnect"):
                if not post_mortem:
                    if command == "next":
                        self.reason = "step"
                        self.set_next(frame)
                    elif command == "stepIn":
                        self.reason = "step"
                        self.set_step()
                    elif command == "stepOut":
                        self.reason = "step"
                        self.set_return(frame)
                    else:
                        self.reason = None
                        self.set_continue()
                self.send({"type": "event", "event": "continued"})
                return
            try:
                if command == "scopes":
                    body = self.scopes(int(message.get("frame", 0)))
                elif command == "variables":
                    body = {"variables": self.children(int(message.get("ref", 0)))}
                elif command == "evaluate":
                    body = self.evaluate(message.get("expression", ""), int(message.get("frame", 0)))
                elif command == "configurationDone":
                    body = {}
                else:
                    body = {"error": "Commande inconnue : %s" % command}
            except Exception as error:
                body = {"error": "%s: %s" % (type(error).__name__, error)}
            self.reply(message, body)

    def frame_at(self, index):
        if not self.frames:
            return None
        return self.frames[max(0, min(index, len(self.frames) - 1))][0]

    def scopes(self, index):
        frame = self.frame_at(index)
        if frame is None:
            return {"locals": []}
        module_level = frame.f_code.co_name == "<module>"
        body = {"locals": self.variables_of(frame.f_locals, module_level)}
        if not module_level:
            body["globals"] = self.variables_of(frame.f_globals, True)
        return body

    def variables_of(self, mapping, hide_private):
        result = []
        for name, value in list(mapping.items()):
            if hide_private and (str(name).startswith("__") or isinstance(value, types.ModuleType)):
                continue
            result.append(self.variable(name, value))
        return result

    def variable(self, name, value):
        reference = 0
        if self.expandable(value):
            reference = self.next_ref
            self.next_ref += 1
            self.refs[reference] = value
        try:
            text = _repr.repr(value)
        except Exception as error:
            text = "<affichage impossible : %s>" % error
        return {"name": str(name), "value": text, "type": type(value).__name__, "ref": reference}

    @staticmethod
    def expandable(value):
        if isinstance(value, SIMPLE) or isinstance(value, NOT_EXPANDED):
            return False
        if isinstance(value, (dict, list, tuple, set, frozenset)):
            return len(value) > 0
        try:
            return bool(vars(value))
        except TypeError:
            return False

    def children(self, reference):
        value = self.refs.get(reference)
        if isinstance(value, dict):
            items = [(_repr.repr(key), item) for key, item in list(value.items())[:MAX_CHILDREN]]
        elif isinstance(value, (list, tuple)):
            items = [("[%d]" % i, item) for i, item in enumerate(value[:MAX_CHILDREN])]
        elif isinstance(value, (set, frozenset)):
            items = [("[%d]" % i, item) for i, item in enumerate(list(value)[:MAX_CHILDREN])]
        elif value is not None:
            try:
                items = list(vars(value).items())[:MAX_CHILDREN]
            except TypeError:
                items = []
        else:
            items = []
        return [self.variable(name, item) for name, item in items]

    def evaluate(self, expression, index):
        frame = self.frame_at(index)
        global_names = frame.f_globals if frame is not None else {}
        local_names = frame.f_locals if frame is not None else {}
        try:
            code = compile(expression, "<console>", "eval")
        except SyntaxError:
            # Instruction (ex. x = 5) plutôt qu'expression
            exec(compile(expression, "<console>", "exec"), global_names, local_names)
            return {"result": "", "type": "", "ref": 0}
        value = eval(code, global_names, local_names)
        variable = self.variable("", value)
        return {"result": variable["value"], "type": variable["type"], "ref": variable["ref"]}

    def post_mortem(self, error):
        frames = []
        tb = error.__traceback__
        while tb is not None:
            frames.append((tb.tb_frame, tb.tb_lineno))
            tb = tb.tb_next
        frames = [(f, line) for f, line in reversed(frames) if self.is_visible(f)]
        if not frames:
            return
        self.stop(frames, "exception", "%s: %s" % (type(error).__name__, error))
        self.command_loop(frames[0][0], post_mortem=True)


def main():
    port = int(sys.argv[1])
    script = os.path.abspath(sys.argv[2])
    sock = socket.create_connection(("127.0.0.1", port))
    session = Session(sock, script)
    threading.Thread(target=session.read_loop, daemon=True).start()

    # Les points d'arrêt arrivent d'abord, puis « configurationDone » lance le programme
    while True:
        message = session.commands.get()
        if message.get("cmd") == "configurationDone":
            session.reply(message, {})
            break
        if message.get("cmd") == "disconnect":
            return
        session.reply(message, {"error": "Le programme n'a pas encore démarré."})

    sys.argv = [script] + sys.argv[3:]
    sys.path[0] = os.path.dirname(script)
    main_globals = {"__name__": "__main__", "__file__": script, "__builtins__": __builtins__,
                    "__package__": None, "__spec__": None}

    exit_code = 0
    try:
        with open(script, "rb") as source:
            code = compile(source.read(), script, "exec")
        session.run(code, main_globals)
    except SystemExit as stop:
        exit_code = stop.code if isinstance(stop.code, int) else (0 if stop.code is None else 1)
        if isinstance(stop.code, str):
            print(stop.code, file=sys.stderr)
    except BaseException as error:
        sys.settrace(None)
        exit_code = 1
        # Trace d'erreur sans les lignes internes du débogueur
        report = traceback.TracebackException.from_exception(error)
        report.stack = traceback.StackSummary.from_list(
            [entry for entry in report.stack
             if not entry.filename.startswith("<")
             and os.path.normcase(os.path.abspath(entry.filename)) != HELPER
             and os.path.basename(entry.filename) != "bdb.py"])
        print("".join(report.format()), file=sys.stderr, end="")
        session.post_mortem(error)
    finally:
        sys.stdout.flush()
        sys.stderr.flush()
        session.send({"type": "event", "event": "terminated", "exitCode": exit_code})
        try:
            sock.close()
        except OSError:
            pass
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
"""";
}
