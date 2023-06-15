namespace Decider;

public class ReservationService
{
    public static void CreateReservation(CreateReservation command)
        => Repository.Apply(command.Id, _ => ReservationAggregate.Create(command));

    public static void ConfirmReservation(ConfirmReservation command) 
        => Repository.Apply(command.Id, aggregate => aggregate!.Confirm(command));
    
    public static void CancelReservation(CancelReservation command) 
        => Repository.Apply(command.Id, aggregate => aggregate!.Cancel(command));
}

public interface ICommand {}
public interface IEvent {}

public enum ReservationStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancel = 3
}

public record CreateReservation(Guid Id, string Number) : ICommand;
public record ConfirmReservation(Guid Id) : ICommand;
public record CancelReservation(Guid Id) : ICommand;

public record ReservationCreated(Guid Id, string Number, ReservationStatus Status) : IEvent;
public record ReservationConfirmed(Guid Id, ReservationStatus Status) : IEvent;
public record ReservationCanceled(Guid Id, ReservationStatus Status) : IEvent;

public class ReservationAggregate
{
    private readonly Guid id;
    private readonly string number;
    private ReservationStatus status;

    public static ReservationAggregate Empty => new();

    private ReservationAggregate() {}
    
    public ReservationAggregate(Guid id, string number, ReservationStatus status)
    {
        this.id = id;
        this.number = number;
        this.status = status;
    }

    public static ReservationCreated Create(CreateReservation command)
    {
        var reservation = new ReservationAggregate(command.Id, command.Number, ReservationStatus.Pending);
        return new ReservationCreated(reservation.id, reservation.number, reservation.status);
    }

    // todo result can be collection of events
    public ReservationConfirmed Confirm(ConfirmReservation command)
    {
        status = ReservationStatus.Confirmed;
        return new ReservationConfirmed(id, status);
    }
    
    public ReservationCanceled Cancel(CancelReservation command)
    {
        status = ReservationStatus.Cancel;
        return new ReservationCanceled(id, status);
    }
}

public class ReservationRecord
{
    public Guid Id { get; private set; }
    public string Number { get; private set; }
    public ReservationStatus Status { get; private set; }

    public void Apply(IEvent @event)
    {
        switch (@event)
        {
            case ReservationCanceled canceled:
                Status = canceled.Status;
                break;
            case ReservationConfirmed confirmed:
                Status = confirmed.Status;
                break;
            case ReservationCreated created:
                Id = created.Id;
                Number = created.Number;
                Status = created.Status;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(@event));
        }
    }
}

public class Repository
{
    public static void Apply(Guid id, Func<ReservationAggregate?, IEvent> action)
    {
        var record = State.Find(id);
        if (record is null)
        {
            var @event = action(null);
            record = new ReservationRecord();
            record.Apply(@event);
            State.Add(record);
        }
        else
        {
            var @event = action(new ReservationAggregate(record.Id, record.Number, record.Status));
            record.Apply(@event);
        }
    }
}

public static class State
{
    private static readonly List<ReservationRecord> Reservations = new();

    public static ReservationRecord? Find(Guid id) => Reservations.FirstOrDefault(x => x.Id == id);

    public static void Add(ReservationRecord reservation) => Reservations.Add(reservation);
}