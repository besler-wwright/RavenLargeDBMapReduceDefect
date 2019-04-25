using System;

namespace RavenDBTestApril2019
{
    public class Charge : CommonStuff
    {

        public Charge() : base()
        {

        }

        public string HostSystem { get; set; }

        public static Charge GenerateRandomCharge()
        {
            var random = new Random();

            var charge = new Charge();
            charge.Id = $"Charge-{Guid.NewGuid():N}";
            charge.PatientId = $"Patient{random.Next(10000)}";
            charge.GLDepartment = $"Dept{random.Next(10)}";
            charge.GLAccount = $"Acct{random.Next(100)}";
            charge.Amount = (decimal)random.Next(10000);
            charge.HostSystem = $"System{random.Next(10)}";

            return charge;
        }
    }
}