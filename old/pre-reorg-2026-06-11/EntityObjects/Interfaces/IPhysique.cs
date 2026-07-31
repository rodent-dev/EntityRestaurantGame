namespace EntityObjects.Interfaces;

public interface IPhysique
{
    double Weight { get; set; }
    double Height { get; set; }
    double FatPercentage { get; set; }
    double MuscleMass { get; set; }
}

public interface IPhysiqueLimb
{
    string LimbName { get; }
    double Strength { get; set; }
    double Dexterity { get; set; }
    double Endurance { get; set; }
}
