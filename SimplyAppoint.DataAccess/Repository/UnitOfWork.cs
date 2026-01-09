using SimplyAppoint.DataAccess.Data;
using SimplyAppoint.DataAccess.Repository.IRepository;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly ApplicationDbContext _db;
        public IBusinessRepository Business { get; private set; }
        public IServiceRepository Service { get; private set; }
        public IBookingPolicyRepository BookingPolicy { get; private set; }
        public IWorkingHoursRepository WorkingHours { get; private set; }
        public ITimeOffRepository TimeOff { get; private set; }
        public IAppointmentRepository Appointment { get; private set; } 
        public IBusinessCustomerRepository BusinessCustomer { get; private set; }
        public UnitOfWork(ApplicationDbContext db)
        {
            _db = db;
            Business = new BusinessRepository(_db);
            Service = new ServiceRepository(_db);
            BookingPolicy = new BookingPolicyRepository(_db);
            WorkingHours = new WorkingHoursRepository(_db);
            TimeOff = new TimeOffRepository(_db);
            Appointment = new AppointmentRepository(_db);
            BusinessCustomer = new BusinessCustomerRepository(_db);

        }

        public void Save()
        {
            _db.SaveChanges();
        }

        public void Dispose()
        {
            _db.Dispose();
        }
    }
}
