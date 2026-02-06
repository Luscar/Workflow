using SimpleBPM;

namespace SimpleBPM.Tests;

public class ConditionDecisionTests
{
    #region Texte

    [Fact]
    public void Evaluer_Texte_Egal_Correspond()
    {
        var condition = new ConditionDecision("statut", "actif", OperateurFiltre.Egal, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object> { { "statut", "actif" } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Texte_Egal_NeCorrespondPas()
    {
        var condition = new ConditionDecision("statut", "actif", OperateurFiltre.Egal, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object> { { "statut", "inactif" } };

        Assert.False(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Texte_Different()
    {
        var condition = new ConditionDecision("statut", "actif", OperateurFiltre.Different, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object> { { "statut", "inactif" } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Texte_Contient()
    {
        var condition = new ConditionDecision("nom", "test", OperateurFiltre.Contient, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object> { { "nom", "mon_test_value" } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Texte_CommencePar()
    {
        var condition = new ConditionDecision("code", "PRE", OperateurFiltre.CommencePar, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object> { { "code", "PREFIX-001" } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Texte_FinitPar()
    {
        var condition = new ConditionDecision("fichier", ".pdf", OperateurFiltre.FinitPar, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object> { { "fichier", "rapport.pdf" } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Texte_Superieur()
    {
        var condition = new ConditionDecision("code", "B", OperateurFiltre.Superieur, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object> { { "code", "C" } };

        Assert.True(condition.Evaluer(variables));
    }

    #endregion

    #region Nombre

    [Fact]
    public void Evaluer_Nombre_Egal()
    {
        var condition = new ConditionDecision("montant", 100.0, OperateurFiltre.Egal, TypeDonnee.Nombre, "node1");
        var variables = new Dictionary<string, object> { { "montant", 100.0 } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Nombre_Superieur()
    {
        var condition = new ConditionDecision("montant", 1000.0, OperateurFiltre.Superieur, TypeDonnee.Nombre, "node1");
        var variables = new Dictionary<string, object> { { "montant", 1500.0 } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Nombre_Superieur_Faux()
    {
        var condition = new ConditionDecision("montant", 1000.0, OperateurFiltre.Superieur, TypeDonnee.Nombre, "node1");
        var variables = new Dictionary<string, object> { { "montant", 500.0 } };

        Assert.False(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Nombre_Inferieur()
    {
        var condition = new ConditionDecision("age", 18, OperateurFiltre.Inferieur, TypeDonnee.Nombre, "node1");
        var variables = new Dictionary<string, object> { { "age", 15 } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Nombre_SuperieurOuEgal()
    {
        var condition = new ConditionDecision("note", 10.0, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre, "node1");
        var variables = new Dictionary<string, object> { { "note", 10.0 } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Nombre_InferieurOuEgal()
    {
        var condition = new ConditionDecision("note", 10.0, OperateurFiltre.InferieurOuEgal, TypeDonnee.Nombre, "node1");
        var variables = new Dictionary<string, object> { { "note", 10.0 } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Nombre_AvecInt()
    {
        var condition = new ConditionDecision("quantite", 5, OperateurFiltre.Egal, TypeDonnee.Nombre, "node1");
        var variables = new Dictionary<string, object> { { "quantite", 5 } };

        Assert.True(condition.Evaluer(variables));
    }

    #endregion

    #region Date

    [Fact]
    public void Evaluer_Date_Egal()
    {
        var date = new DateTime(2026, 1, 15);
        var condition = new ConditionDecision("echeance", date, OperateurFiltre.Egal, TypeDonnee.Date, "node1");
        var variables = new Dictionary<string, object> { { "echeance", date } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Date_Superieur()
    {
        var condition = new ConditionDecision("echeance", new DateTime(2026, 1, 1), OperateurFiltre.Superieur, TypeDonnee.Date, "node1");
        var variables = new Dictionary<string, object> { { "echeance", new DateTime(2026, 6, 15) } };

        Assert.True(condition.Evaluer(variables));
    }

    #endregion

    #region Booleen

    [Fact]
    public void Evaluer_Booleen_Egal_Vrai()
    {
        var condition = new ConditionDecision("estActif", true, OperateurFiltre.Egal, TypeDonnee.Booleen, "node1");
        var variables = new Dictionary<string, object> { { "estActif", true } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Booleen_Egal_Faux()
    {
        var condition = new ConditionDecision("estActif", true, OperateurFiltre.Egal, TypeDonnee.Booleen, "node1");
        var variables = new Dictionary<string, object> { { "estActif", false } };

        Assert.False(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_Booleen_Different()
    {
        var condition = new ConditionDecision("estActif", true, OperateurFiltre.Different, TypeDonnee.Booleen, "node1");
        var variables = new Dictionary<string, object> { { "estActif", false } };

        Assert.True(condition.Evaluer(variables));
    }

    #endregion

    #region Cas limites

    [Fact]
    public void Evaluer_VariableAbsente_RetourneFaux()
    {
        var condition = new ConditionDecision("inexistant", "valeur", OperateurFiltre.Egal, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object>();

        Assert.False(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_DeuxNulls_AvecEgal_RetourneVrai()
    {
        var condition = new ConditionDecision("var", null, OperateurFiltre.Egal, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object> { { "var", null! } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Evaluer_UnNull_AvecDifferent_RetourneVrai()
    {
        var condition = new ConditionDecision("var", "valeur", OperateurFiltre.Different, TypeDonnee.Texte, "node1");
        var variables = new Dictionary<string, object> { { "var", null! } };

        Assert.True(condition.Evaluer(variables));
    }

    [Fact]
    public void Constructor_ParDefaut()
    {
        var condition = new ConditionDecision();

        Assert.Equal(string.Empty, condition.NomVariable);
        Assert.Null(condition.Valeur);
        Assert.Equal(OperateurFiltre.Egal, condition.Operateur);
        Assert.Equal(TypeDonnee.Texte, condition.TypeDonnee);
        Assert.Equal(string.Empty, condition.NoeudCible);
    }

    #endregion
}
