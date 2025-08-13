namespace DemoLegal.Domain;

/// <summary>Тип должника (определяется по "Тип ЛС").</summary>
public enum DebtorType
{
    Person = 0,  // собственник-физлицо
    Company = 1  // застройщик/юрлицо
}

/// <summary>Статус дела по конвейеру.</summary>
public enum CaseStatus
{
    Candidate = 0,   // черновик/кандидат после импорта
    Pretrial = 1,    // досудебка
    CourtOrder = 2,  // судебный приказ
    Lawsuit = 3,     // иск
    Fssp = 4         // исполнительное производство
}
