namespace SimpleBPM;

public enum OperateurFiltre
{
    Egal,
    Different,
    Superieur,
    SuperieurOuEgal,
    Inferieur,
    InferieurOuEgal,
    Contient,
    CommencePar,
    FinitPar
}

public enum TypeDonnee
{
    Texte,
    Nombre,
    Date,
    Booleen
}

public class FiltreVariable
{
    public string NomVariable { get; set; } = string.Empty;
    public object? Valeur { get; set; }
    public OperateurFiltre Operateur { get; set; } = OperateurFiltre.Egal;
    public TypeDonnee TypeDonnee { get; set; } = TypeDonnee.Texte;

    public FiltreVariable() { }

    public FiltreVariable(string nomVariable, object? valeur, OperateurFiltre operateur = OperateurFiltre.Egal, TypeDonnee typeDonnee = TypeDonnee.Texte)
    {
        NomVariable = nomVariable;
        Valeur = valeur;
        Operateur = operateur;
        TypeDonnee = typeDonnee;
    }

    internal bool Correspond(object? valeurVariable)
    {
        if (valeurVariable == null && Valeur == null)
            return Operateur == OperateurFiltre.Egal;

        if (valeurVariable == null || Valeur == null)
            return Operateur == OperateurFiltre.Different;

        return TypeDonnee switch
        {
            TypeDonnee.Texte => ComparerTexte(valeurVariable.ToString(), Valeur.ToString()),
            TypeDonnee.Nombre => ComparerNombre(valeurVariable, Valeur),
            TypeDonnee.Date => ComparerDate(valeurVariable, Valeur),
            TypeDonnee.Booleen => ComparerBooleen(valeurVariable, Valeur),
            _ => false
        };
    }

    private bool ComparerTexte(string? valeurVariable, string? valeurFiltre)
    {
        valeurVariable ??= string.Empty;
        valeurFiltre ??= string.Empty;

        return Operateur switch
        {
            OperateurFiltre.Egal => valeurVariable.Equals(valeurFiltre, StringComparison.Ordinal),
            OperateurFiltre.Different => !valeurVariable.Equals(valeurFiltre, StringComparison.Ordinal),
            OperateurFiltre.Contient => valeurVariable.Contains(valeurFiltre, StringComparison.Ordinal),
            OperateurFiltre.CommencePar => valeurVariable.StartsWith(valeurFiltre, StringComparison.Ordinal),
            OperateurFiltre.FinitPar => valeurVariable.EndsWith(valeurFiltre, StringComparison.Ordinal),
            OperateurFiltre.Superieur => string.Compare(valeurVariable, valeurFiltre, StringComparison.Ordinal) > 0,
            OperateurFiltre.SuperieurOuEgal => string.Compare(valeurVariable, valeurFiltre, StringComparison.Ordinal) >= 0,
            OperateurFiltre.Inferieur => string.Compare(valeurVariable, valeurFiltre, StringComparison.Ordinal) < 0,
            OperateurFiltre.InferieurOuEgal => string.Compare(valeurVariable, valeurFiltre, StringComparison.Ordinal) <= 0,
            _ => false
        };
    }

    private bool ComparerNombre(object valeurVariable, object valeurFiltre)
    {
        if (!TryConvertToDouble(valeurVariable, out var numVariable) ||
            !TryConvertToDouble(valeurFiltre, out var numFiltre))
            return false;

        return Operateur switch
        {
            OperateurFiltre.Egal => Math.Abs(numVariable - numFiltre) < double.Epsilon,
            OperateurFiltre.Different => Math.Abs(numVariable - numFiltre) >= double.Epsilon,
            OperateurFiltre.Superieur => numVariable > numFiltre,
            OperateurFiltre.SuperieurOuEgal => numVariable >= numFiltre,
            OperateurFiltre.Inferieur => numVariable < numFiltre,
            OperateurFiltre.InferieurOuEgal => numVariable <= numFiltre,
            _ => false
        };
    }

    private bool ComparerDate(object valeurVariable, object valeurFiltre)
    {
        if (!TryConvertToDateTime(valeurVariable, out var dateVariable) ||
            !TryConvertToDateTime(valeurFiltre, out var dateFiltre))
            return false;

        return Operateur switch
        {
            OperateurFiltre.Egal => dateVariable == dateFiltre,
            OperateurFiltre.Different => dateVariable != dateFiltre,
            OperateurFiltre.Superieur => dateVariable > dateFiltre,
            OperateurFiltre.SuperieurOuEgal => dateVariable >= dateFiltre,
            OperateurFiltre.Inferieur => dateVariable < dateFiltre,
            OperateurFiltre.InferieurOuEgal => dateVariable <= dateFiltre,
            _ => false
        };
    }

    private bool ComparerBooleen(object valeurVariable, object valeurFiltre)
    {
        if (!TryConvertToBool(valeurVariable, out var boolVariable) ||
            !TryConvertToBool(valeurFiltre, out var boolFiltre))
            return false;

        return Operateur switch
        {
            OperateurFiltre.Egal => boolVariable == boolFiltre,
            OperateurFiltre.Different => boolVariable != boolFiltre,
            _ => false
        };
    }

    private static bool TryConvertToDouble(object value, out double result)
    {
        result = 0;
        if (value is double d) { result = d; return true; }
        if (value is int i) { result = i; return true; }
        if (value is long l) { result = l; return true; }
        if (value is decimal dec) { result = (double)dec; return true; }
        if (value is float f) { result = f; return true; }
        if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            result = je.GetDouble();
            return true;
        }
        return double.TryParse(value.ToString(), out result);
    }

    private static bool TryConvertToDateTime(object value, out DateTime result)
    {
        result = DateTime.MinValue;
        if (value is DateTime dt) { result = dt; return true; }
        if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return DateTime.TryParse(je.GetString(), out result);
        }
        return DateTime.TryParse(value.ToString(), out result);
    }

    private static bool TryConvertToBool(object value, out bool result)
    {
        result = false;
        if (value is bool b) { result = b; return true; }
        if (value is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.True) { result = true; return true; }
            if (je.ValueKind == System.Text.Json.JsonValueKind.False) { result = false; return true; }
        }
        return bool.TryParse(value.ToString(), out result);
    }
}
