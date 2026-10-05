namespace Sage.Kahoot.Domain.Quizzes;

/// <summary>
/// Faixas normativas de quiz-management. Fonte única: usadas pelo domínio e pelos validators da borda,
/// para que a regra de corte de D2 não faça as duas camadas divergirem.
/// </summary>
public static class QuizRules
{
    public const int TitleMinLength = 1;
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 500;

    public const int QuestionTextMinLength = 1;
    public const int QuestionTextMaxLength = 300;

    public const int OptionTextMaxLength = 120;
    public const int MinOptionsPerQuestion = 2;
    public const int MaxOptionsPerQuestion = 4;

    public const int MinTimeLimitSeconds = 5;
    public const int MaxTimeLimitSeconds = 120;

    public const int MinBasePoints = 100;
    public const int MaxBasePoints = 2000;
}
