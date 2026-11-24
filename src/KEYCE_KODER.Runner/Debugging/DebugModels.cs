namespace KEYCE_KODER.Runner.Debugging;

/// <summary>État d'une session de débogage.</summary>
public enum DebugState
{
    Idle,      // aucune session
    Starting,  // lancement du programme
    Running,   // le programme s'exécute
    Paused     // arrêté sur un point d'arrêt, un pas à pas ou une pause
}

/// <summary>Un point d'arrêt envoyé au débogueur : ligne (à partir de 1) et condition facultative.</summary>
public sealed record SourceBreakpoint(int Line, string? Condition = null);

/// <summary>Une ligne de la pile d'appels (Id = 0 pour la fonction en cours).</summary>
public sealed record DebugFrame(int Id, string Name, string FilePath, int Line)
{
    public override string ToString() => $"{Name}    {Path.GetFileName(FilePath)} : {Line}";
}

/// <summary>Une variable. Reference > 0 : la variable a un contenu à déplier (liste, dictionnaire, objet).</summary>
public sealed record DebugVariable(string Name, string Value, string Type, int Reference)
{
    public bool HasChildren => Reference > 0;
}

/// <summary>Variables visibles depuis une ligne de la pile : locales et (dans une fonction) globales.</summary>
public sealed record DebugScopes(IReadOnlyList<DebugVariable> Locals, IReadOnlyList<DebugVariable>? Globals);

/// <summary>Résultat de l'évaluation d'une expression (console de débogage, espions).</summary>
public sealed record DebugEvaluation(string Result, string Type, int Reference, string? Error)
{
    public bool Failed => Error is not null;
}

/// <summary>
/// Le programme s'est arrêté.
/// Reason : « breakpoint », « step », « pause » ou « exception ».
/// </summary>
public sealed record DebugStoppedEventArgs(string Reason, string? Description, IReadOnlyList<DebugFrame> Frames);
