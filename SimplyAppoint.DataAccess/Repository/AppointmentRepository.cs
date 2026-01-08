using SimplyAppoint.DataAccess.Data;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Linq;

namespace SimplyAppoint.DataAccess.Repository
{
    public class AppointmentRepository : Repository<Appointment>, IAppointmentRepository
    {
        private readonly ApplicationDbContext _db;
        public AppointmentRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(Appointment obj)
        {
            var apptFromDb = _db.Appointments.FirstOrDefault(a => a.Id == obj.Id);
            if (apptFromDb == null) return;

            apptFromDb.StartUtc = obj.StartUtc;
            apptFromDb.EndUtc = obj.EndUtc;

            apptFromDb.CustomerName = obj.CustomerName;
            apptFromDb.CustomerEmail = obj.CustomerEmail;
            apptFromDb.CustomerPhone = obj.CustomerPhone;

            apptFromDb.Status = obj.Status;
            apptFromDb.Notes = obj.Notes;
            apptFromDb.CancelledUtc = obj.CancelledUtc;
            apptFromDb.CancellationReason = obj.CancellationReason;
        }
    }
}
