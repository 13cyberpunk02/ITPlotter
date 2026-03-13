namespace ITPlotter.Domain.Enums;

public enum PrinterStatus
{
    Idle,
    Printing,
    PaperJam,
    OutOfPaper,
    OutOfToner,
    OutOfInk,
    Error,
    Offline
}
