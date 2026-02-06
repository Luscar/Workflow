namespace SimpleBPM;

public class ConditionDecision
{
    public string NomVariable { get; set; } = string.Empty;
    public object? Valeur { get; set; }
    public OperateurFiltre Operateur { get; set; } = OperateurFiltre.Egal;
    public TypeDonnee TypeDonnee { get; set; } = TypeDonnee.Texte;
    public string NoeudCible { get; set; } = string.Empty;

    public ConditionDecision() { }

    public ConditionDecision(string nomVariable, object? valeur, OperateurFiltre operateur, TypeDonnee typeDonnee, string noeudCible)
    {
        NomVariable = nomVariable;
        Valeur = valeur;
        Operateur = operateur;
        TypeDonnee = typeDonnee;
        NoeudCible = noeudCible;
    }

    public bool Evaluer(Dictionary<string, object> variables)
    {
        if (!variables.TryGetValue(NomVariable, out var valeurVariable))
            return false;

        return Correspond(valeurVariable);
    }

    private bool Correspond(object? valeurVariable)
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

    private bool ComparerTexte(string? valeurVariable, string? valeurCondition)
    {
        valeurVariable ??= string.Empty;
        valeurCondition ??= string.Empty;

        return Operateur switch
        {
            OperateurFiltre.Egal => valeurVariable.Equals(valeurCondition, StringComparison.Ordinal),
            OperateurFiltre.Different => !valeurVariable.Equals(valeurCondition, StringComparison.Ordinal),
            OperateurFiltre.Contient => valeurVariable.Contains(valeurCondition, StringComparison.Ordinal),
            OperateurFiltre.CommencePar => valeurVariable.StartsWith(valeurCondition, StringComparison.Ordinal),
            OperateurFiltre.FinitPar => valeurVariable.EndsWith(valeurCondition, StringComparison.Ordinal),
            OperateurFiltre.Superieur => string.Compare(valeurVariable, valeurCondition, StringComparison.Ordinal) > 0,
            OperateurFiltre.SuperieurOuEgal => string.Compare(valeurVariable, valeurCondition, StringComparison.Ordinal) >= 0,
            OperateurFiltre.Inferieur => string.Compare(valeurVariable, valeurCondition, StringComparison.Ordinal) < 0,
            OperateurFiltre.InferieurOuEgal => string.Compare(valeurVariable, valeurCondition, StringComparison.Ordinal) <= 0,
            _ => false
        };
    }

    private bool ComparerNombre(object valeurVariable, object valeurCondition)
    {
        if (!TryConvertToDouble(valeurVariable, out var numVariable) ||
            !TryConvertToDouble(valeurCondition, out var numCondition))
            return false;

        return Operateur switch
        {
            OperateurFiltre.Egal => Math.Abs(numVariable - numCondition) < double.Epsilon,
            OperateurFiltre.Different => Math.Abs(numVariable - numCondition) >= double.Epsilon,
            OperateurFiltre.Superieur => numVariable > numCondition,
            OperateurFiltre.SuperieurOuEgal => numVariable >= numCondition,
            OperateurFiltre.Inferieur => numVariable < numCondition,
            OperateurFiltre.InferieurOuEgal => numVariable <= numCondition,
            _ => false
        };
    }

    private bool ComparerDate(object valeurVariable, object valeurCondition)
    {
        if (!TryConvertToDateTime(valeurVariable, out var dateVariable) ||
            !TryConvertToDateTime(valeurCondition, out var dateCondition))
            return false;

        return Operateur switch
        {
            OperateurFiltre.Egal => dateVariable == dateCondition,
            OperateurFiltre.Different => dateVariable != dateCondition,
            OperateurFiltre.Superieur => dateVariable > dateCondition,
            OperateurFiltre.SuperieurOuEgal => dateVariable >= dateCondition,
            OperateurFiltre.Inferieur => dateVariable < dateCondition,
            OperateurFiltre.InferieurOuEgal => dateVariable <= dateCondition,
            _ => false
        };
    }

    private bool ComparerBooleen(object valeurVariable, object valeurCondition)
    {
        if (!TryConvertToBool(valeurVariable, out var boolVariable) ||
            !TryConvertToBool(valeurCondition, out var boolCondition))
            return false;

        return Operateur switch
        {
            OperateurFiltre.Egal => boolVariable == boolCondition,
            OperateurFiltre.Different => boolVariable != boolCondition,
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
