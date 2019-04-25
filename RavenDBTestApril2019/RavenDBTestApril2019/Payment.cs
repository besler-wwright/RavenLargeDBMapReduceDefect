using System;

namespace RavenDBTestApril2019
{
    public class Payment:CommonStuff
    {
        public Payment():base()
        {
            
        }

        public string ACH { get; set; }
        public DateTime ACHReceivedDate { get; set; }

        public static Payment GenerateNewFromCharge(Charge charge)
        {
            var random = new Random();

            var p = new Payment();
            p.Id = $"Payment-{Guid.NewGuid():N}";
            p.PatientId = charge.PatientId;
            p.GLDepartment = charge.GLDepartment;
            p.GLAccount= charge.GLAccount;
            p.PostedDate = DateTime.Now.Subtract(TimeSpan.FromDays(0 - random.Next(365)));
            p.ACHReceivedDate = p.PostedDate.Value.Add(TimeSpan.FromDays(random.Next(5)));

            var chanceToNotBeSame = random.Next(4);
            if (chanceToNotBeSame == 1)
            {
                p.Amount = (decimal)0.00 - random.Next(10000); //payments are always negative
            }
            else
            {
                p.Amount = (decimal)0.00 - charge.Amount; //payments are always negative
            }
            
            
            return p;

        }
    }
}