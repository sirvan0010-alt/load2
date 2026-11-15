namespace MailLoadTester;

public readonly record struct ProgressUpdate(
    int Sent,
    int Failed,
    string Status,
    double? EtaSeconds,
    int Phase,
    string CurrentStep,
    string NextStep,
    TestPhase TestPhase = TestPhase.Idle,
    MessageStep? MessageStep = null,
    int? MessageIndex = null,
    int? WorkerId = null,
    int WorkerCount = 0,
    /// <summary>Aktuální krok cesty odesílání (pro pipeline GUI, která se zbarvuje).</summary>
    DeliveryStepKind? PathStep = null,
    /// <summary>Zda poslední path krok uspěl (true=zelená, false=červená).</summary>
    bool? PathStepSuccess = null,
    /// <summary>Lidský text stavu tempa (burst pauza, greylist, time window…).</summary>
    string? PaceStatus = null,
    /// <summary>Efektivní interval ms po jitter/backoff.</summary>
    int? EffectiveIntervalMs = null,
    /// <summary>Agregované pozorované SMTP odpovědi (pro živou tabulku v GUI).</summary>
    IReadOnlyList<ObservedResponseRow>? ObservedSnapshot = null,
    /// <summary>Conservatively reserved SMTP-prepared MIME bytes for a capacity run.</summary>
    long? CapacityMimeBytesReserved = null,
    /// <summary>Configured cumulative MIME-byte budget; null means no total byte ceiling.</summary>
    long? CapacityMimeBytesTarget = null,
    /// <summary>Actual SMTP SendAsync calls started, including retries.</summary>
    int SmtpSendAttempts = 0,
    /// <summary>Observed transient SMTP 4xx responses.</summary>
    int Smtp4xx = 0,
    /// <summary>Observed permanent SMTP 5xx responses.</summary>
    int Smtp5xx = 0,
    /// <summary>Current adaptive concurrency limit; null when adaptive control is disabled.</summary>
    int? AdaptiveConcurrency = null);
