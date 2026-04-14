public enum EnvironmentCheckType
{
    None,           // risposta statica (usa correctIndex)
    WindowsCount,   // risposta = numero finestre attive
    StudentsCount,  // risposta = numero studenti visibili
    EmptyDesksCount // risposta = numero studenti spariti
}