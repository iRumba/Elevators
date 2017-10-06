using System;
using Api;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections;

namespace client
{
    public class strategy : baseStrategy
    {
        static strategy _strategy;

        void Log(string msg)
        {
            log(msg);
            //WriteToConsole(msg);

        }

        void WriteToConsole(string msg)
        {
            msg = $"Tick{Game.Counter}: {msg}";
            
            Console.WriteLine(msg);
        }

        void WriteToFile(string msg)
        {
            File.AppendAllText("log.txt", $"{msg}{Environment.NewLine}");
        }

        public static void WriteLog(string msg)
        {
            _strategy?.Log(msg);
        }

        public override void onTick(List<Passenger> myPassengers, List<Elevator> myElevators, List<Passenger> enemyPassengers, List<Elevator> enemyElevators)
        {
            Game.CurrentMyPassengers = myPassengers;
            Game.CurrentEnemyPassengers = enemyPassengers;
            Game.CurrentMyElevators = myElevators;
            Game.CurrentEnemyElevators = enemyElevators;

            Clearing();
            Distribute();
            ManageElevators();
            ManagePassengers();
        }

        private void Clearing()
        {
            Game.InvitedPassengers.Clear();
            Game.InvitedByElevators = null;
            Game.CanInvite.Clear();
            Game.CanInviteByPass.Clear();
        }

        void AtFirst()
        {
            Game.MyType = Game.CurrentMyElevators.First().Type;
            _strategy = this;
        }

        void ManageElevators()
        {
            var free = Game.CurrentMyElevators.Where(el => el.IsFilling());
            var stand = free.ToList();
            var canMove = free.ToList();
            foreach (var elev in free)
            {
                if (elev.IsWaitPassengers())
                {
                    canMove.Remove(elev);
                }
                else if (elev.IsFull())
                {
                    if (elev.GoToNextFloor())
                        stand.Remove(elev);

                    continue;
                }

                //if (elev.GetAllPassengers().Count() >= elev.GetCapacity())
                //{
                //    stand.Remove(elev);
                //    canMove.Remove(elev);
                //    continue;
                //}

                //if (elev.GetGoingOnPassengers().Count() > 0)
                //{
                //    canMove.Remove(elev);
                //}

                //IEnumerable<Passenger> forInvite;
                //Elevator nearestEnemyElev = null;
                //var enemyElevsOnFloor = Game.CurrentEnemyElevators.Where(el => el.Floor == elev.Floor && el.State == (int)ElevStates.Filling);
                //if (enemyElevsOnFloor.Count() > 0)
                //{
                //    var min = enemyElevsOnFloor.Select(e => Math.Abs(e.GetX())).Min();
                //    nearestEnemyElev = enemyElevsOnFloor.FirstOrDefault(el => Math.Abs(el.GetX()) == min);
                //}

                //if (nearestEnemyElev != null)
                //    forInvite = Game.PassengersOnFloors[elev.Floor].GetReady().Where(p => p.GetDistanceTo(nearestEnemyElev) >= p.GetDistanceTo(elev));
                //else
                //    forInvite =
                //        elev.PassengersWhoEncreaseBounty();

                //foreach (var fi in forInvite)
                //{
                //    if (!Game.CanInvite.ContainsKey(elev))
                //        Game.CanInvite[elev] = new List<Passenger>();
                //    Game.CanInvite[elev].Add(fi);

                //    if (!Game.CanInviteByPass.ContainsKey(fi))
                //        Game.CanInviteByPass[fi] = new List<Elevator>();
                //    Game.CanInviteByPass[fi].Add(elev);
                //}
            }

            ManageInvites();

            //WriteLog($"{Game.InvitedPassengers.Count}");

            foreach(var elev in free.Where(elev=>!Game.GoingOnElevators[elev]))
            {
                if (!elev.HasInvitedPassengers())
                {
                    elev.GoToNextFloor();
                }  
            }
        }

        void ManageInvites()
        {
            var floors = new SortedSet<int>(Game.CurrentMyElevators.Where(elev=>elev.IsFilling() && !Game.GoingOnElevators[elev])
                .Select(elev => elev.Floor));
            //WriteLog($"Floors: {floors.Count}");
            foreach (var f in floors)
            {
                // Находим пачки пассажиров с приростом очков на тик для каждого лифта
                var passengersOnFloor = Game.PassengersOnFloors[f].GetReady().GetByDestFloors();
                //WriteLog($"passengersOnFloor: {passengersOnFloor.Count}");
                var pwcCollection = new List<PassengersWithCostForElevators>();
                foreach (var pof in passengersOnFloor)
                {
                    var passengersWithCosts = new PassengersWithCostForElevators(passengersOnFloor[pof.Key]);
                    if (passengersWithCosts.IncreasedCosts.Any(ic => ic.CostWith > ic.CostWithout))
                        pwcCollection.Add(passengersWithCosts);


                }

                foreach (var pwc in pwcCollection.OrderByDescending(pwc=>pwc.IncreasedCosts.Select(ic=>ic.CostWith - ic.CostWithout).Max()))
                {
                    var elevsCosts = pwc.IncreasedCosts.Where(ec => ec.CostWith > ec.CostWithout).OrderByDescending(ec => ec.CostWith - ec.CostWithout);
                    //WriteLog($"elevsCosts: {elevsCosts.Count()}");
                    foreach (var ec in elevsCosts)
                    {
                        var elevFreeSpace = ec.Elevator.FreeSpace() - ec.Elevator.InvitedCount();
                        if (elevFreeSpace >= pwc.Passengers.Count)
                        {
                            //WriteLog("All Invite");
                            pwc.Passengers.ForEach(p => Game.InvitedPassengers[p] = ec.Elevator);
                            break;
                        }
                        var mine = pwc.Passengers.Where(p => p.IsMine()).ToList();
                        if (elevFreeSpace >= mine.Count)
                        {
                            //WriteLog("Mine Invite");
                            mine.ForEach(p => Game.InvitedPassengers[p] = ec.Elevator);
                            break;
                        }
                    }
                }

                

                //var elevatorsOnFloor = elevators.Where(elev => elev.Floor == f);
                //var buf = new List<Passenger>();

                //// Первичное распределение.
                //// Назначаем новых пассажиров в лифты, которые едут на те же этажи
                //foreach(var elev in elevatorsOnFloor)
                //{
                //    var byDestFloors = Game.CanInvite[elev].Where(p=>!p.IsInvited()).GetByDestFloors();
                //    var elevDestFloors = elev.GetAllPassengers().GetDestFloors();
                //    foreach(var f2 in elevDestFloors)
                //    {
                //        foreach (var p in byDestFloors[f2])
                //            Game.InvitedPassengers[p] = elev;
                //    }
                //}

                //// Вторичный проход. 
                //// Устраняем переполнение. Сначала убираем пассажиров с максимальной дистанцией
                //foreach (var elev in elevatorsOnFloor)
                //{
                //    if (elev.FreeSpace() >= elev.InvitedCount())
                //        continue;

                //    var forDel = elev.GetInvitedPassengers().OrderBy(p => Math.Abs(p.DestFloor - elev.Floor)).Take(elev.FreeSpace());
                //    foreach(var p in forDel)
                //    {
                //        Game.InvitedPassengers.Remove(p);
                //        //if (CanInviteByPass[p].Count > 1)
                //        //    buf.Add(p);
                //    }
                //}

                //// Третий проход.
                //// Подбираем пассажиров, с которыми по пути.
                //foreach (var elev in elevatorsOnFloor)
                //{
                //    if (elev.FreeSpace() == elev.InvitedCount())
                //        continue;

                //    var elevFloorsAverage = elev.GetAllPassengers().GetFloorsAverage();
                //    if (elevFloorsAverage == 0)
                //        elevFloorsAverage = elev.Floor;
                //    IEnumerable<Passenger> freePassengersToWay = Game.CanInvite[elev]
                //        .Where(p => p.GetDirection() == elev.GetDirection() || elev.GetDirection() == Direction.Unknown)
                //        .Select(p => new { Pass = p, Dist = Math.Abs(p.DestFloor - elevFloorsAverage) })
                //        .OrderBy(item => item.Dist).Select(item => item.Pass);

                //    var byDestFloors = freePassengersToWay.GetByDestFloors();
                //    var floorsByMax = byDestFloors
                //        .Select(k => new { Floor = k.Key, Count = byDestFloors[k.Key].Count() });
                //    //.Select(item=>item.Floor);

                //    var elevFree = elev.FreeSpace() - elev.InvitedCount();
                //    foreach (var f2 in floorsByMax)
                //    {
                //        var mustPickup = f2.Count <= elevFree;
                //        if (!mustPickup)
                //            continue;

                //        foreach (var p in byDestFloors[f2.Floor])
                //        {
                //            if (elevFree > 0)
                //            {
                //                elevFree--;
                //                Game.InvitedPassengers[p] = elev;
                //            }
                //        }
                //    }
                //}
            }
        }

        void ManagePassengers()
        {
            foreach (var pass in Game.CurrentAllPassengers)
            {
                if (Game.InvitedPassengers.ContainsKey(pass))
                {
                    pass.SetElevator(Game.InvitedPassengers[pass]);
                }
            }
        }

        void Distribute()
        {
            Game.Counter++;
            if (Game.Counter == 1)
                AtFirst();

            if (Game.Counter % 100 == 0)
                WriteLog($"{Game.Counter}");

            Game.GoingOnElevators = Game.CurrentMyElevators.ToDictionary(elev => elev, elev => false);
            Game.CurrentAllPassengers = Game.CurrentMyPassengers.Concat(Game.CurrentEnemyPassengers).ToList();
            Game.PassengersOnFloors = Game.CurrentAllPassengers.ToLookup(p => p.Floor);
            Game.CurrentAllElevators = Game.CurrentMyElevators.Concat(Game.CurrentEnemyElevators).ToList();
            Game.ActivePassengers = Game.CurrentAllPassengers.Where(p => p.State != 4 && p.State != 6).ToList();

            foreach (var elev in Game.CurrentAllElevators)
            {
                if (!Game.FillingTime.ContainsKey(elev))
                    Game.FillingTime[elev] = 0;
                if (elev.State == (int)ElevStates.Filling)
                    Game.FillingTime[elev]++;
                else
                    Game.FillingTime[elev] = 0;
            }

            ManageVisitedFloors();

            ManageExpectedPassengers();
        }

        private void ManageExpectedPassengers()
        {
            // Получаем Id активных пассажиров
            var activePassengersIds = Game.ActivePassengers.Select(p => p.Id);

            // Удаляем активных пассажиров из ожидаемых (они уже не ожидаются, они вышли из тени :) )
            Game.ExpectedPassengers.RemoveAll(ep => ep.Id.HasValue && activePassengersIds.Contains(ep.Id.Value));

            // Добавляем в ожидаемых тех пассажиров, которые зашли на лестницу или вышли из лифта, если их еще там нет
            var expectedPassengersIds = Game.ExpectedPassengers.Where(p => p.Id.HasValue).Select(p => p.Id).ToList();
            var exitingPassengers = Game.CurrentAllPassengers.Where(p => p.State == 6 && !expectedPassengersIds.Contains(p.Id));
            var movingPassengers = Game.CurrentAllPassengers.Where(p => p.State == 4 && !expectedPassengersIds.Contains(p.Id));
            Game.ExpectedPassengers.AddRange(exitingPassengers.Select(p => new ExpectedPassengerInfo
            {
                Id = p.Id,
                Floor = p.Floor,
                IsMine = p.GetElevator().IsMine(),
                Tick = p.GetTimeForRespawn() + Game.Counter,
                Weight = p.Weight,
            }));
            Game.ExpectedPassengers.AddRange(movingPassengers.Select(p => new ExpectedPassengerInfo
            {
                Id = p.Id,
                Floor = p.DestFloor,
                IsMine = p.IsMine(),
                Tick = p.GetTimeForRespawn() + Game.Counter,
                Weight = p.Weight,
            }));

            FillExpectedPassengersLookup();
        }

        private void ManageVisitedFloors()
        {
            foreach (var p in Game.CurrentAllPassengers)
            {
                if (!Game.VisitedFloors.ContainsKey(p.Id))
                    Game.VisitedFloors[p.Id] = new HashSet<int>();
                if (p.Floor != 1)
                    Game.VisitedFloors[p.Id].Add(p.Floor);
            }
        }

        void FillExpectedPassengersLookup()
        {
            Game.ExpectedPassengersOnFloors = Game.ExpectedPassengers.Concat(GetExpectedPassengersOnFirst()).OrderBy(ep => ep.Tick).ToLookup(pi => pi.Floor, pi => pi);
        }

        IEnumerable<ExpectedPassengerInfo> GetExpectedPassengersOnFirst()
        {
            var startTick = Game.Counter - Game.Counter % Game.Rules.PassCreatingInterval + Game.Rules.PassCreatingInterval;
            for (var i = startTick; i <= Game.Rules.LastPassCreatingTime; i += Game.Rules.PassCreatingInterval)
            {
                yield return new ExpectedPassengerInfo
                {
                    Floor = 1,
                    Id = null,
                    IsMine = true,
                    Tick = i
                };

                yield return new ExpectedPassengerInfo
                {
                    Floor = 1,
                    Id = null,
                    IsMine = false,
                    Tick = i
                };
            }
        }


    }

    public static class Game
    {
        static Dictionary<string, int> _intCache = new Dictionary<string, int>();
        static Dictionary<string, Way> _jumpsCache = new Dictionary<string, Way>();

        public static List<ExpectedPassengerInfo> ExpectedPassengers = new List<ExpectedPassengerInfo>();
        public static int Counter = 0;
        public static int[] AllFloors { get; } = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        public static List<Passenger> CurrentMyPassengers { get; set; }
        public static List<Passenger> CurrentEnemyPassengers { get; set; }
        public static List<Elevator> CurrentMyElevators { get; set; }
        public static List<Elevator> CurrentEnemyElevators { get; set; }
        public static List<Passenger> CurrentAllPassengers { get; set; }
        public static List<Elevator> CurrentAllElevators { get; set; }
        public static ILookup<int, ExpectedPassengerInfo> ExpectedPassengersOnFloors { get; set; }
        public static ILookup<int, Passenger> PassengersOnFloors { get; set; }
        public static List<Passenger> ActivePassengers { get; set; }
        public static GameRules Rules { get; } = new GameRules();
        public static Dictionary<Passenger, Elevator> InvitedPassengers = new Dictionary<Passenger, Elevator>();
        public static Dictionary<Elevator, List<Passenger>> CanInvite = new Dictionary<Elevator, List<Passenger>>();
        public static Dictionary<Passenger, List<Elevator>> CanInviteByPass = new Dictionary<Passenger, List<Elevator>>();
        public static ILookup<Elevator, Passenger> InvitedByElevators { get; set; }
        public static Dictionary<int, HashSet<int>> VisitedFloors { get; set; } = new Dictionary<int, HashSet<int>>();
        public static int MyCoordMultiplier => MyType.StartsWith("F") ? -1 : 1;
        public static string MyType { get; set; }
        public static List<PassengersWithCostForElevators> PassengersWhoMayIncrease { get; set; }
        public static Dictionary<Elevator, bool> GoingOnElevators { get; set; }
        public static bool Flag1 { get; private set; }
        public static Dictionary<Elevator, int> FillingTime { get; set; } = new Dictionary<Elevator, int>();

        public static bool IsFilling(this Elevator elev)
        {
            return elev.State == (int)ElevStates.Filling;
        }

        public static int GetCost(this IEnumerable<PartOfPassenger> passengers)
        {
            return passengers.Select(p => p.GetCost()).Sum();
        }

        public static int GetCost(this PartOfPassenger pass)
        {
            return Math.Abs(pass.FromFloor - pass.DestFloor) * pass.GetCostMultiplier();
        }

        public static int GetCostMultiplier(this PartOfPassenger pass)
        {
            return GetCostMultiplier(pass.IsMine);
        }

        public static int GetCost(this IEnumerable<Passenger> passengers)
        {
            return passengers.Select(p => p.GetCost()).Sum();
        }

        public static int GetCost(this Passenger pass)
        {
            return Math.Abs(pass.FromFloor - pass.DestFloor) * pass.GetCostMultiplier();
        }

        public static int GetCostMultiplier(this Passenger pass)
        {
            return GetCostMultiplier(pass.IsMine());
        }

        public static float GetAverageCost(this ExpectedPassengerInfo ep)
        {
            var multiplier = ep.IsMine ? Rules.CostPerFloorForMy : Rules.CostPerFloorForEnemy;
            var bounties = new List<int>();
            foreach (var i in ep.GetExpectedPassengerFloors())
            {
                bounties.Add(Math.Abs(i - ep.Floor) * multiplier);
            }
            return (float)bounties.Average();
        }

        public static float GetAverageCost(this IEnumerable<ExpectedPassengerInfo> eps)
        {
            return eps.Select(ep => ep.GetAverageCost()).Average();
        }

        public static int GetCostMultiplier(this ExpectedPassengerInfo pass)
        {
            return GetCostMultiplier(pass.IsMine);
        }

        public static int GetCostMultiplier(bool isMine)
        {
            return isMine ? Rules.CostPerFloorForMy : Rules.CostPerFloorForEnemy;
        }

        public static SortedSet<int> GetDestFloors(this IEnumerable<PartOfPassenger> passengers)
        {
            return new SortedSet<int>(passengers.Select(p => p.DestFloor));
        }

        public static float GetDistanceTo(this Passenger pass, Elevator elev)
        {
            return Math.Abs(pass.X - elev.GetX());
        }

        public static float GetFloorsAverage(this IEnumerable<Passenger> passengers)
        {
            var destFloors = passengers.Select(p => p.DestFloor);
            if (destFloors.Count() == 0)
                return 0;
            return (float)destFloors.Average();
        }

        public static Direction GetDirection(this Elevator elev)
        {
            if (elev.NextFloor > 0)
            {
                if (elev.NextFloor > elev.Floor)
                    return Direction.Up;
                else
                    return Direction.Down;
            }
            var floors = elev.GetAllPassengers().GetDestFloors().ToArray();
            if (floors.Length == 0)
                return Direction.Unknown;
            if (floors.All(f => f >= elev.Floor))
                return Direction.Up;
            if (floors.All(f => f <= elev.Floor))
                return Direction.Down;
            return Direction.Unknown;
        }

        public static Direction GetDirection(this Passenger pass)
        {
            if (pass.FromFloor < pass.DestFloor)
                return Direction.Up;
            return Direction.Down;
        }

        public static Direction GetDirection(this ExpectedPassengerInfo ep)
        {
            if (ep.Floor == 1)
                return Direction.Up;
            if (ep.Floor == 9)
                return Direction.Down;
            if (ep.Id.HasValue && VisitedFloors.ContainsKey(ep.Id.Value) && VisitedFloors[ep.Id.Value].Count == 5)
                return Direction.Down;
            return Direction.Unknown;
        }

        public static int GetCapacity(this Elevator elev)
        {
            return Rules.ElevsCapacity;
        }

        public static int FreeSpace(this Elevator elev)
        {
            return elev.GetCapacity() - elev.GetAllPassengers().Count();
        }

        public static int InvitedCount(this Elevator elev)
        {
            return InvitedPassengers.Where(kv => kv.Value == elev).Count();
        }

        public static IEnumerable<Passenger> GetInvitedPassengers(this Elevator elev)
        {
            return InvitedPassengers.Where(kv => kv.Value == elev).Select(kv => kv.Key);
        }

        public static bool IsMoving(this Elevator elev)
        {
            return elev.State == (int)ElevStates.Moving || elev.State == (int)ElevStates.Waiting;
        }

        //public static Dictionary<int, float> GetFloorsWithExpectedByTick(this Elevator elev)
        //{
        //    var res = new Dictionary<int, float>();
        //    var nextFloor = elev.ComputeNextFloor();
        //    IEnumerable<int> checkedFloors;
        //    if (nextFloor > 0)
        //        checkedFloors = AllFloors.Between(elev.Floor, nextFloor);
        //    else
        //        checkedFloors = AllFloors;

        //    foreach(var f in checkedFloors)
        //    {
        //        var timeWhen = elev.GetTickWhenElevatorWillOnFloor(f);
        //        var toPassCount = elev.Passengers.GetByDestFloors()[f].Count();
        //        var elevsTo = CurrentAllElevators
        //            .Where(el => el.IsMoving() && elev.NextFloor == f && elev.GetTickWhenElevatorWillOnFloor(f) < timeWhen);
        //        if (elevsTo.Count() > 0 && toPassCount == 0)
        //            continue;
        //        var expectedTicks = ExpectedPassengersOnFloors[f].Where(ep => ep.Tick > timeWhen)
        //            .Where(ep => ep.GetDirection() == elev.GetDirection() || elev.GetDirection() == Direction.Unknown)
        //            .Select(ep => ep.Tick + (ep.IsMine ? 0 : Rules.MinStandingTimeForPickupEnemy))
        //            .ToArray();
        //        if (expectedTicks.Length == 0 && toPassCount == 0)
        //            continue;

        //        var lastExpectedTime = expectedTicks.Length > 0 ? expectedTicks.Max() : Counter + 1;

        //        var value = (float)(expectedTicks.Length + toPassCount) / (lastExpectedTime - Counter);
                    
        //        res.Add(f, value);
        //    }

        //    return res;
        //}

        public static bool HasInvitedPassengers(this Elevator elev)
        {
            return InvitedPassengers.ContainsValue(elev);
        }

        public static bool IsWaitPassengers(this Elevator elev)
        {
            return PassengersOnFloors[elev.Floor].Any(p => p.HasElevator() && p.Elevator == elev.Id && p.State == (int)PassStates.MovingToElevator);
        }

        public static bool IsReadyToMove(this Elevator elev)
        {
            return Counter<140 ? elev.TimeOnFloor >= Rules.MinElevsStandingTime : elev.TimeOnFloor >= Rules.MinElevsStandingTime + Rules.OpeningDoorsTime;
        }

        public static int Nearest(this IEnumerable<int> src, int dest)
        {
            var minDist = src.Select(i => Math.Abs(i - dest)).Min();
            return src.First(i => Math.Abs(i - dest) == minDist);
        }

        public static IEnumerable<int> Between(this IEnumerable<int> src, int from, int to)
        {
            return src.Where(s => s.IsBetween(from, to));
        }

        public static bool GoToNextFloor(this Elevator elev)
        {
            if (!elev.IsReadyToMove())
                return false;
            if (elev.GetGoingOnPassengers().Count() > 0)
                return false;
            var next = elev.GetBestFloorWithPassengers();

            if (next > 0 && next != elev.Floor)
            {
                elev.GoToFloor(next);
                GoingOnElevators[elev] = true;
                return true;
            }
            //if (elev.Floor == 9 && elev.GetAllPassengers().Count() == 0 && elev.IsReadyToMove())
            //    Console.WriteLine($"NEXT: {next}");
            return false;
        }

        public static IEnumerable<ExpectedPassengerInfo> GetExpectedBetweenOnFloor(int floor, int from, int to)
        {
            return ExpectedPassengersOnFloors[floor].Where(ep => ep.Tick.IsBetween(from, to));
        }

        public static bool IsCloserToMid(this Elevator elev, Elevator other)
        {
            return Math.Abs(elev.GetX()) < Math.Abs(other.GetX());
        }

        public static bool IsOposed(this Elevator elev, Elevator other)
        {
            return Math.Abs(elev.GetX()) == Math.Abs(other.GetX());
        }

        public static int GetPotentialRejects(this Elevator elev)
        {
            var elevsOnFloor = CurrentAllElevators.Where(el => el.Floor == elev.Floor && el.State == (int)ElevStates.Filling);
            // Лифты, которые ближе 
            var elevsCloserToMid = elevsOnFloor.Where(el => el.IsCloserToMid(elev));
            // Сумма свободных мест на лифтах ближе
            var closerRejects = elevsCloserToMid.Sum(el => el.FreeSpace());
            // Лифты на противоположной стороне (вообще это один лифт. Позже поменяю)
            var oposedElevs = elevsOnFloor.Where(el => el.IsOposed(elev));
            // количество свободных мест на лифте напротив
            var oposedRejects = oposedElevs.Sum(el => el.FreeSpace());
            // Из этого количества вычтем количество чужих пассажиров (потому что свои пойдут к себе, если позвать)
            oposedRejects -= PassengersOnFloors[elev.Floor].Where(p => !p.IsMine()).Count();
            return closerRejects + oposedRejects;
        }

        public static int TimeForMyPassengerGoingTo(this Elevator elev)
        {
            return (int)Math.Ceiling((float)(Math.Abs(elev.GetX()) - Rules.FromMidToWaitingDistance) / Rules.PassMovingSpeed);
        }

        public static int TimeForEnemyPassengerGoingTo(this Elevator elev)
        {
            return (int)Math.Ceiling((float)(Math.Abs(elev.GetX()) + Rules.FromMidToWaitingDistance) / Rules.PassMovingSpeed);
        }

        public static IEnumerable<FloorsWithPassengers> GetFloorsWithPassengers(this Elevator elev)
        {
            //var preNext = elev.ComputeNextFloor();
            //throw new NotImplementedException();

            //IEnumerable<int> rangeFloors = null;
            //if (preNext > 0)
            //{
            //    rangeFloors = Calculator.GetRange(elev.Floor, preNext);
            //}
            //else
            //{
            //    rangeFloors = AllFloors;
            //}

            //var currentRejects = elev.GetPotentialRejects();
            //var way = new Way(elev);
            //var cc = way.GetCostPerTick();

            foreach (var floor in AllFloors)
            {
                if (floor == elev.Floor)
                    continue;

                var timeWhen = Calculator.CalculateJumpTime(elev.GetAllPassengers(), elev.Floor, floor) + elev.GetTimeWhenAllPassengersWillInside();

                var tickWhen = timeWhen + Counter;

                var waitings = PassengersOnFloors[floor].GetReady().Where(p => p.TimeToAway - (p.IsMine() ? elev.TimeForMyPassengerGoingTo() : elev.TimeForEnemyPassengerGoingTo()) > timeWhen);
                //var byDest = waitings.GetByDestFloors();

                var expected = ExpectedPassengersOnFloors[floor].Where(p => p.Tick <= tickWhen);

                yield return new FloorsWithPassengers
                {
                    Floor = floor,
                    WaitingPassengers = waitings.ToList(),
                    ExpectedPassengers = expected.ToList(),
                    TimeWhen = timeWhen,
                    ExpectedsOnCurrent = ExpectedPassengersOnFloors[elev.Floor].Where(ep=>ep.Tick <= tickWhen).ToList(),
                };
            }
        }

        public static int GetBestFloorWithPassengers(this Elevator elev)
        {
            // Узнать, куда собирается лифт с текущими пассажирами. Если никуда, то ищем наилучший этаж где можно зашибать бабло.
            // Если куда то едет, то смотрим, есть ли смысл тормозить по дороге.
            var preNext = elev.ComputeNextFloor();
            if (elev.IsFull())
                return preNext;

            var floorsWithPassengers = elev.GetFloorsWithPassengers();
                //.Where(f => !elev.GetAllPassengers().GetDestFloors().Contains(f.Floor));

            //if (preNext > 0)
            //{
            //    var range = Calculator.GetRange(elev.Floor, preNext).ToList();
            //    floorsWithPassengers = floorsWithPassengers.Where(item => range.Contains(item.Floor));
            //}



            var rejected = GetPotentialRejects(elev);

            //var expecteds = ExpectedPassengersOnFloors[elev.Floor];

            var way = new Way(elev);
            var cc = way.GetCostPerTick();

            var filtered = new List<FloorsWithPassengers>();

            var dict = new Dictionary<int, float>();

            foreach(var item in floorsWithPassengers)
            {
                //if (Counter>2000 && !Flag1)
                //{
                //    Console.WriteLine($"{item.}")
                //    Flag1 = true;
                //}
                //if (elev.Floor == 9)
                //    Console.WriteLine($"Floor: {item.Floor} WaitingsCount: {item.WaitingPassengers.Count} ExpectedCount: {item.ExpectedPassengers.Count} ExpOnCurrentFloor: {item.ExpectedsOnCurrent.Count}");
                var byDest = item.WaitingPassengers.GetByDestFloors();
                //Console.WriteLine($"Tick: {Counter} ID: {elev.Id} ByDest: {byDest.Count}");
                var eLand = new Landing
                {
                    Floor = elev.Floor,
                    Passengers = item.ExpectedsOnCurrent.OrderBy(ep => ep.Tick).Skip(rejected).Select(ep => ep.ToPart()),
                };
                //Console.WriteLine($"{eLand.Passengers.Count()}");
                var eWay = new Way(elev, new Landing[] { eLand });
                var eCpt = eWay.GetCostPerTick();
                foreach (var dest in byDest)
                {
                    var wLand = new Landing
                    {
                        Floor = dest.Key,
                        Passengers = byDest[dest.Key].ToPart(),
                    };
                    var wWay = new Way(elev, new Landing[] { wLand });
                    var wCpt = wWay.GetCostPerTick() - eCpt;

                    //if (wCpt > 0)
                    //    Console.WriteLine("GOOD!");

                    if (!dict.ContainsKey(item.Floor))
                        dict[item.Floor] = int.MinValue;
                    if (wCpt > dict[item.Floor])
                        dict[item.Floor] = wCpt;
                }
            }
            var res = dict.Where(kv => kv.Value >= cc && kv.Value > 0)
                .OrderByDescending(kv => kv.Value).Select(kv => kv.Key);
            //var res = floorsWithPassengers.ToDictionary(item => item.Floor,
            //    item => new Way(elev, new Landing[]
            //    {
            //        new Landing
            //        {
            //            Floor = item.Floor,
            //            Passengers = item.WaitingPassengers.ToPart().Concat(item.ExpectedPassengers.Select(ep => ep.ToPart()))
            //        }
            //    }).GetCostPerTick() - new Way(elev, new Landing[]
            //    {
            //        new Landing
            //        {
            //            Floor = elev.Floor,
            //            Passengers = item.ExpectedsOnCurrent.OrderBy(ep => ep.Tick).Skip(rejected).Select(ep => ep.ToPart()),
            //        }
            //    }).GetCostPerTick())
            //    .Where(kv => kv.Value >= cc && kv.Value > 0)
            //    .OrderByDescending(kv => kv.Value).Select(kv => kv.Key);

            foreach(var floor in res)
            {
                var other = CurrentAllElevators.Where(el => el != elev && el.NextFloor == floor && el.IsMoving() && el.IsCloserToMid(elev));
                if (other.Count() == 0)
                {
                    //if (elev.GetAllPassengers().GetByDestFloors()[floor].Count() == 0 && floor != elev.Floor && elev.IsEmpty())
                    //{
                    //    Console.WriteLine($"FUCK!!! Tick: {Counter} ID: {elev.Id} Floor: {floor} Val: {dict[floor]} OnThis: {cc} Expected: {ExpectedPassengersOnFloors[floor].Select(ep => ep.Tick).Count()}");
                    //    Console.WriteLine($"Waitings: {floorsWithPassengers.First(fwp => fwp.Floor == floor).WaitingPassengers.Count}");
                    //    Console.WriteLine($"WaitingsByFloors: {floorsWithPassengers.First(fwp => fwp.Floor == floor).WaitingPassengers.GetByDestFloors().Count}");
                    //    Console.WriteLine($"Expected: {floorsWithPassengers.First(fwp => fwp.Floor == floor).ExpectedPassengers.Count}");
                    //    Console.WriteLine($"ExpectedOnCurrent: {floorsWithPassengers.First(fwp => fwp.Floor == floor).ExpectedsOnCurrent.Count}");
                    //    Console.WriteLine($"Floors With Passengers: {floorsWithPassengers.Count()}");
                    //    Console.WriteLine($"Dict: {dict.Count}");
                    //    Console.WriteLine($"Res: {res.Count()}");
                    //    Console.WriteLine($"PreNext: {preNext}");
                    //    //Console.ReadKey();
                    //}

                    return floor;
                }
                    
            }

            //if (preNext==-1 && elev.Floor == 9)
            //{
            //    Console.WriteLine($"floorsWithPassengers COUNT: {floorsWithPassengers.Count()} RES COUNT: {res.Count()}");
            //}
            //if (elev.IsEmpty() && elev.Floor == 1 && preNext > 0)
            //    Console.WriteLine($"ID: {elev.Id} Passengers: {elev.Passengers.Count} AllPassengers: {elev.GetAllPassengers().Count()} GoingOn: {elev.GetGoingOnPassengers().Count()}");
            return preNext;
            //foreach (var item in floorsWithPassengers)
            //{
            //    var expectedsOnCurrent = expecteds.Where(ep => ep.Tick < item.TimeWhen + Counter);
            //    var toWay = new Way(elev, new Landing[] { new Landing { Floor = item.Floor, Passengers = item.WaitingPassengers.ToPart() } });
                
            //    var byDestFloors = item.WaitingPassengers.GetByDestFloors();
            //    foreach (var destFloor in byDestFloors)
            //    {
            //        var passOnFloor = byDestFloors[destFloor.Key];
            //    }
            //}
        }

        public static int GetTimeWhenAllPassengersWillInside(this Elevator elev)
        {
            var goingOn = elev.GetGoingOnPassengers();
            if (goingOn.Count() == 0)
                return 0;

            return (int)Math.Ceiling(goingOn.Select(p => p.GetDistanceTo(elev)).Max() / Rules.PassMovingSpeed);
        }

        public static Dictionary<int, float> GetFloorsWithWaitingsByTick(this Elevator elev)
        {
            var res = new Dictionary<int, float>();
            var nextFloor = elev.ComputeNextFloor();
            IEnumerable<int> checkedFloors;
            if (nextFloor > 0)
                checkedFloors = AllFloors.Between(elev.Floor, nextFloor);
            else
                checkedFloors = AllFloors;

            var toPass = elev.GetAllPassengers().GetByDestFloors();
            foreach (var f in checkedFloors)
            {
                var timeWhen = elev.GetTickWhenElevatorWillOnFloor(f);
                var toPassCount = toPass[f].Count();
                var elevsTo = CurrentAllElevators.Where(el => el.IsMoving() && elev.NextFloor == f && elev.GetTickWhenElevatorWillOnFloor(f) < timeWhen);
                if (elevsTo.Count() > 0 && toPassCount == 0)
                    continue;
                var waitings = PassengersOnFloors[f].GetReady()
                    .Where(p=> p.GetDirection() == elev.GetDirection() || p.GetDirection() == Direction.Unknown)
                    .Where(p => p.TimeToAway + Counter - (p.IsMine() ? 0 : Rules.MinStandingTimeForPickupEnemy) > timeWhen);
                if (waitings.Count() == 0 && toPassCount == 0)
                    continue;

                if (timeWhen == Counter)
                    timeWhen++;


                var value = (float)(waitings.Count() + toPassCount) / (timeWhen - Counter);
                //if (float.IsInfinity(value) || float.IsNaN(value))
                //{
                //    Console.WriteLine($"{waitings.Count()} {timeWhen} {strategy.Counter}");
                //    Console.ReadKey();
                //}
                res.Add(f, value);
            }
            return res;
            //var res = new Dictionary<int, float>();
            //for (var i = 1; i < 10; i++)
            //{
            //    //if (i == elev.Floor)
            //    //    continue;

            //    var time = elev.GetTickWhenElevatorWillOnFloor(i);
            //    var waitingPassengers = strategy.PassengersOnFloors[i].GetReady().Where(p => p.GetTickWhenAway() > time);
            //    var expectedPassengers = strategy.ExpectedPassengersOnFloors[i].Where(ep => ep.Tick + strategy.Rules.PassWaitingTime > time);
            //    res.Add(i, waitingPassengers.Count() + expectedPassengers.Count());
            //}
            //return res;
        }

        public static int GetTickWhenAway(this Passenger pass)
        {
            return pass.TimeToAway + Counter;
        }

        public static bool IsEmpty(this Elevator elev)
        {
            return elev.Passengers.Where(p => p.State != (int)PassStates.UsingElevator).Count() == 0;
        }

        public static IEnumerable<int> GetExpectedPassengerFloors(this ExpectedPassengerInfo ep)
        {
            if (!ep.Id.HasValue)
                return Calculator.GetRange(2, 10);

            if (VisitedFloors[ep.Id.Value].Count == 5)
                return new int[] { 1 };

            return AllFloors.Where(f => !VisitedFloors[ep.Id.Value].Contains(f) && f != 1);
        }

        public static int GetTickWhenElevatorWillOnFloor(this Elevator elev, int floor)
        {
            var cacheKey = $"{nameof(GetTickWhenElevatorWillOnFloor)}{Counter}{elev.Id}{floor}";
            if (_intCache.ContainsKey(cacheKey))
                return _intCache[cacheKey];
            if (elev.IsMoving())
            {
                if (elev.NextFloor == floor)
                    return _intCache[cacheKey] = (int)(Math.Ceiling(Math.Abs(elev.Y - elev.NextFloor) * elev.Speed)) + Counter + Rules.OpeningDoorsTime;
                return _intCache[cacheKey] = -1;
            }
            var res = Counter;
            if (elev.Floor == floor)
                return _intCache[cacheKey] = res;


            var next = elev.ComputeNextFloor();
            if (floor.IsBetween(elev.Floor, next))
            {
                var canMoveThrough = 0;
                var passTime = elev.GetGoingOnPassengers()
                    .Select(p => (int)Math.Ceiling((float)Math.Abs(p.X - elev.GetX()) / Rules.PassMovingSpeed));
                if (passTime.Count() > 0)
                    canMoveThrough = passTime.Max();
                canMoveThrough = Math.Max(canMoveThrough, Rules.MinElevsStandingTime - elev.TimeOnFloor);

                var ticksByFloor = floor < elev.Floor ? Rules.ElevsDowningTime : Rules.MinElevsUppingTime * elev.GetAllPassengers().GetWeightMultiplier();

                return _intCache[cacheKey] = (int)Math.Ceiling(ticksByFloor * Math.Abs(floor - elev.Floor)) + Rules.OpeningDoorsTime + Rules.ClosingDoorsTime + Counter;
            }
            return -1;
        }

        public static Way GetWay(this Elevator elev)
        {
            var cahceKey = $"{nameof(GetWay)}{Counter}{elev.Id}";
            if (_jumpsCache.ContainsKey(cahceKey))
                return _jumpsCache[cahceKey];

            return _jumpsCache[cahceKey] = new Way(elev);
        }

        public static float GetWeightMultiplier(this IEnumerable<Passenger> passengers)
        {
            var res = 1f;
            foreach (var p in passengers)
                res *= p.Weight;

            if (passengers.Count() > Rules.ElevsOverloadValue)
                res *= Rules.OverloadMultiplier;
            return res;
        }

        public static float GetWeightMultiplier(this IEnumerable<ExpectedPassengerInfo> passengers)
        {
            var res = 1f;
            foreach (var p in passengers)
                res *= p.Weight;

            if (passengers.Count() > Rules.ElevsOverloadValue)
                res *= Rules.OverloadMultiplier;
            return res;
        }

        public static float GetWeightMultiplier(this IEnumerable<PartOfPassenger> passengers)
        {
            var res = 1f;
            var count = 0;
            foreach (var p in passengers)
            {
                res *= p.Weight;
                count++;
            }
            if (count > Rules.ElevsOverloadValue)
                res *= Rules.OverloadMultiplier;
                
            return res;
        }

        public static IEnumerable<Passenger> GetReady(this IEnumerable<Passenger> passengers)
        {
            return passengers.Where(p => p.State == (int)PassStates.WaitingForElevator || p.State == (int)PassStates.Returning);
        }

        public static void ForEach<T>(this IEnumerable<T> items, Action<T> act)
        {
            foreach (var item in items)
                act?.Invoke(item);
        }

        public static Elevator GetElevator(this Passenger pass)
        {
            return CurrentAllElevators.FirstOrDefault(el => el.Id == pass.Elevator);
        }

        public static IEnumerable<Passenger> GetGoingOnPassengers(this Elevator elev)
        {
            return CurrentAllPassengers.Where(p => p.Elevator == elev.Id && p.State == (int)PassStates.MovingToElevator);
        }

        public static IEnumerable<Passenger> GetAllPassengers(this Elevator elev)
        {
            return elev.Passengers.Concat(elev.GetGoingOnPassengers());
        }

        public static bool IsMine(this Elevator elev)
        {
            return elev.Type == MyType;
        }

        public static bool IsMine(this Passenger pass)
        {
            return pass.Type == MyType;
        }

        public static int GetTimeForRespawn(this Passenger pass)
        {
            switch (pass.State)
            {
                case (int)PassStates.MovingToFloor:
                    var width = Math.Abs(pass.DestFloor - pass.Y);
                    if (pass.GetDirection() == Direction.Unknown)
                        return -1;

                    var timeByFloor = pass.GetDirection() == Direction.Up ? Rules.PassUppingTime : Rules.PassDowningTime;
                    return (int)Math.Ceiling(timeByFloor * width) + Rules.PassWalkingTime + 1;
                case (int)PassStates.Exiting:
                    return Rules.AfterLeavingMovingTime - FillingTime[pass.GetElevator()] + Rules.PassWalkingTime + 1;
                default:
                    return -1;
            }
        }

        public static int ComputeNextFloor(this Elevator elev)
        {
            return elev.GetWay().NextFloor;
        }

        public static SortedSet<int> GetDestFloors(this IEnumerable<Passenger> passengers)
        {
            var res = new SortedSet<int>();
            passengers.ForEach(p => res.Add(p.DestFloor));
            return res;
        }

        public static int GetX(this Elevator elev)
        {
            var index = elev.IsMine() ? CurrentMyElevators.IndexOf(elev) : CurrentEnemyElevators.IndexOf(elev);
            var m = elev.Type.StartsWith("F") ? -1 : 1;
            return (index * Rules.BetweenElvesDistance + Rules.FromMidToElvesDistance) * m;
        }

        public static int WillFillingOnFloorBeforeTicks(this Elevator elev)
        {
            if (elev.State != (int)ElevStates.Moving)
                return -1;
            return (int)Math.Ceiling(Math.Abs(elev.NextFloor - elev.Y) / elev.Speed) + Rules.OpeningDoorsTime;
        }

        public static bool CanPickupEnemy(this Elevator elev)
        {
            return FillingTime[elev] >= Rules.MinStandingTimeForPickupEnemy;
        }

        public static bool IsInvited(this Passenger pass)
        {
            return InvitedPassengers.ContainsKey(pass);
        }

        public static bool IsInvitedAll(this IEnumerable<Passenger> passengers)
        {
            return passengers.All(InvitedPassengers.ContainsKey);
        }

        public static bool IsInvitedAny(this IEnumerable<Passenger> passengers)
        {
            return passengers.Any(InvitedPassengers.ContainsKey);
        }

        public static ILookup<int, Passenger> GetByDestFloors(this IEnumerable<Passenger> passengers)
        {
            return passengers.ToLookup(p => p.DestFloor);
        }

        public static IEnumerable<Passenger> PassengersWhoEncreaseBounty(this Elevator elev)
        {
            var res = new List<Passenger>();
            var readyPassengers = PassengersOnFloors[elev.Floor].GetReady();
            var floors = readyPassengers.GetDestFloors();
            var byFloors = readyPassengers.GetByDestFloors();
            var way = elev.GetWay();
            var cc = way.GetCostPerTick();
            foreach (var f in floors)
            {
                var wayWith = new Way(elev.GetAllPassengers().Concat(byFloors[f].Take(elev.FreeSpace())), elev.Floor);
                if (wayWith.GetCostPerTick() >= cc)
                    res.AddRange(byFloors[f]);
            }

            return res;
        }

        public static int GetX(this ExpectedPassengerInfo ep)
        {
            return Rules.FromMidToWaitingDistance * ep.GetCoordMultiplier();
        }

        public static int GetCoordMultiplier(this ExpectedPassengerInfo ep)
        {
            return ep.IsMine ? MyCoordMultiplier : -1 * MyCoordMultiplier;
        }

        public static PartOfPassenger ToPart(this ExpectedPassengerInfo ep)
        {
            var canFloors = ep.GetExpectedPassengerFloors().ToArray();
            var floorsAverage = (int)Math.Round(canFloors.Average(), 0);
            var res = floorsAverage;
            if (!canFloors.Contains(res))
                res = canFloors.Nearest(res);
            return ep.ToPart(res);
        }

        public static PartOfPassenger ToPart(this ExpectedPassengerInfo ep, int destFloor)
        {
            return new PartOfPassenger(ep, destFloor);
        }

        public static IEnumerable<PartOfPassenger> ToPart(this IEnumerable<Passenger> passengers)
        {
            return passengers.Select(p => new PartOfPassenger(p));
        }

        public static bool IsFull(this Elevator elev)
        {
            return elev.Passengers.Count == Rules.ElevsCapacity;
        }
    }

    public class Jump
    {
        public int FromFloor { get; set; }
        public int ToFloor { get; set; }
        public IEnumerable<PartOfPassenger> Passengers { get; set; }
        public int Time => Calculator.CalculateJumpTime(Passengers, FromFloor, ToFloor);
        public int Cost => Passengers.Where(p => p.DestFloor == ToFloor).GetCost();
    }

    public class Way : IEnumerable<Jump>
    { 
        List<Jump> _items;
        int _start;

        public int Time { get; set; }

        public int Cost { get; set; }

        public Way(Elevator elev, IEnumerable<Landing> landings = null) : this(elev.GetAllPassengers(), elev.Floor, landings) { }

        public Way(IEnumerable<Passenger> passengers, int start, IEnumerable<Landing> landings = null) : this (passengers.ToPart(), start, landings) { }

        public Way(IEnumerable<PartOfPassenger> passengers, int start, IEnumerable<Landing> landings = null)
        {
            var allWays = GetWays(passengers, start, landings?.Where(l => l.Passengers.FirstOrDefault() != null));
            var exAllWays = new List<List<Jump>>();

            foreach (var way in allWays)
            {
                var exWay = way.ToList();
                while (GetWayTime(exWay) + Game.Counter > 7200)
                    exWay.RemoveAt(exWay.Count - 1);
                exAllWays.Add(exWay);
            }

            var minTime = int.MaxValue;
            List<Jump> minWay = null;
            foreach (var way in exAllWays)
            {
                var time = GetWayTime(way);
                if (time < minTime)
                {
                    minTime = time;
                    minWay = way;
                }
            }

            _items = minWay ?? new List<Jump>();

            Time = minTime;

            Cost = GetWayCost(_items);

            _start = start;
        }

        public int NextFloor
        {
            get
            {
                return _items.FirstOrDefault()?.ToFloor ?? -1;
            }
        }

        IEnumerable<IEnumerable<Jump>> GetWays(IEnumerable<PartOfPassenger> passengers, int start, IEnumerable<Landing> landings)
        {
            var floors = passengers.GetDestFloors().Where(f => f != start);
            var passengersWithLandings = passengers;

            if (landings != null)
            {
                var landingsOnFloor = landings.Where(l => l.Floor == start);

                foreach (var land in landingsOnFloor)
                {
                    passengersWithLandings = passengersWithLandings.Concat(land.Passengers);
                    floors = floors.Concat(land.Passengers.GetDestFloors());
                }
                floors = floors.Concat(landings.Where(l => l.Floor != start).Select(l => l.Floor));
                floors = new SortedSet<int>(floors);

                landings = landings.Where(l => l.Floor != start);
            }
            var passengersToNext = passengersWithLandings.Where(p => p.DestFloor != start);

            var more = new List<int>();
            var less = new List<int>();
            foreach (var f in floors)
            {
                if (f > start)
                    more.Add(f);
                else if (f < start)
                    less.Add(f);
            }
            var about = new List<int>();
            if (more.Count > 0)
                about.Add(more.Min());
            if (less.Count > 0)
                about.Add(less.Max());

            foreach (var i in about)
            {
                var jumpsCol = GetWays(passengersToNext, i, landings);
                var jump = new Jump
                {
                    FromFloor = start,
                    ToFloor = i,
                    Passengers = passengersWithLandings,
                };
                var retJumps = new Jump[] { jump }.AsEnumerable();
                foreach (var jumps in jumpsCol)
                {

                    retJumps = retJumps.Concat(jumps);
                    yield return retJumps;
                }
                if (jumpsCol.Count() == 0)
                    yield return retJumps;
            }
        }

        int GetWayTime(IEnumerable<Jump> jumps)
        {
            return jumps.Sum(j => j.Time);
        }

        int GetWayCost(IEnumerable<Jump> jumps)
        {
            return jumps.Sum(j => j.Cost);
        }

        public IEnumerator<Jump> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        internal float GetCostPerTick()
        {
            if (Cost == 0)
                return 0;
            return (float)Cost / Time;
        }
    }

    public static class Calculator
    {
        public static float GetElevUppingSpeedWithPassengers(IEnumerable<Passenger> passengers)
        {
            return passengers.GetWeightMultiplier() * Game.Rules.MinElevsUppingTime;
        }

        public static float GetElevUppingSpeedWithPassengers(IEnumerable<ExpectedPassengerInfo> passengers)
        {
            return passengers.GetWeightMultiplier() * Game.Rules.MinElevsUppingTime;
        }

        public static float GetElevUppingSpeedWithPassengers(IEnumerable<PartOfPassenger> passengers)
        {
            return passengers.GetWeightMultiplier() * Game.Rules.MinElevsUppingTime;
        }

        public static IEnumerable<int> GetRange(this IEnumerable<int> src, int mid, bool withoutMid = true)
        {
            var moreThenMid = new List<int>();
            var lessThenMid = new List<int>();
            foreach (var i in src)
            {
                if (i > mid)
                    moreThenMid.Add(i);
                else if (i < mid)
                    lessThenMid.Add(i);
                else if (!withoutMid)
                    yield return i;
            }

            var startWithMore = moreThenMid.Count <= lessThenMid.Count;
            Func<int?> fm = () =>
            {
                if (moreThenMid.Count == 0)
                    return null;
                var min = moreThenMid.Min();
                moreThenMid.Remove(min);
                return min;
            };

            Func<int?> fl = () =>
            {
                if (lessThenMid.Count == 0)
                    return null;
                var max = lessThenMid.Max();
                lessThenMid.Remove(max);
                return max;
            };

            Func<int?> first;
            Func<int?> second;
            if (startWithMore)
            {
                first = fm;
                second = fl;
            }
            else
            {
                first = fl;
                second = fm;
            }

            while (moreThenMid.Count > 0 || lessThenMid.Count > 0)
            {
                var f = first();
                if (f.HasValue)
                    yield return f.Value;
                var s = second();
                if (s.HasValue)
                    yield return s.Value;
            }
        }

        public static IEnumerable<int> GetRange(int from, int to, int step = 1)
        {
            if (from > to)
            {
                for (var i = from; i > to; i -= step)
                    yield return i;
            }
            else if (from < to)
            {
                for (var i = from; i < to; i += step)
                    yield return i;
            }
        }

        public static bool IsBetween(this int src, int from, int to)
        {
            var f = Math.Min(from, to);
            var t = Math.Max(from, to);

            return src <= to && src >= from;
        }

        public static bool IsBetween(this float src, float from, float to)
        {
            var f = Math.Min(from, to);
            var t = Math.Max(from, to);

            return src <= to && src >= to;
        }

        public static int CalculateJumpTime(IEnumerable<PartOfPassenger> passengers, int fromFloor, int destFloor)
        {
            var movingTime = fromFloor > destFloor ? Game.Rules.ElevsDowningTime : Game.Rules.MinElevsUppingTime * passengers.GetWeightMultiplier();
            var res = Game.Rules.ClosingDoorsTime + (int)Math.Ceiling(movingTime) + Game.Rules.OpeningDoorsTime;
            return res;
        }

        public static int CalculateJumpTime(IEnumerable<Passenger> passengers, int fromFloor, int destFloor)
        {
            if (fromFloor == destFloor)
                return 0;
            var movingTime = fromFloor > destFloor ? Game.Rules.ElevsDowningTime : Game.Rules.MinElevsUppingTime * passengers.GetWeightMultiplier();
            var res = Game.Rules.ClosingDoorsTime + (int)Math.Ceiling(movingTime) + Game.Rules.OpeningDoorsTime;
            return res;
        }

        public static int CalculateJumpTime(IEnumerable<ExpectedPassengerInfo> passengers, int fromFloor, int destFloor)
        {
            var movingTime = fromFloor > destFloor ? Game.Rules.ElevsDowningTime : Game.Rules.MinElevsUppingTime * passengers.GetWeightMultiplier();
            var res = Game.Rules.ClosingDoorsTime + (int)Math.Ceiling(movingTime) + Game.Rules.OpeningDoorsTime;
            return res;
        }

        public static int CalculateJumpTime(IEnumerable<PartOfPassenger> passengers, IEnumerable<int> floors, int startFloor, int adding = 0)
        {
            if (floors.Count() == 0)
                return 0;
            var nextFloor = floors.First();
            var res = CalculateJumpTime(passengers, startFloor, nextFloor);
            res += CalculateJumpTime(passengers.Where(p => p.DestFloor != nextFloor), floors.Where(f => f != nextFloor), nextFloor, Game.Rules.MinElevsStandingTime);
            return res;
        }

        public static int CalculateCost(IEnumerable<PartOfPassenger> passengers)
        {
            return passengers.Sum(p => Math.Abs(p.DestFloor - p.FromFloor) * (p.IsMine ? Game.Rules.CostPerFloorForMy : Game.Rules.CostPerFloorForEnemy));
        }
    }

    public class ExpectedPassengerInfo
    {
        public int? Id { get; set; }
        public int Tick { get; set; }
        public int Floor { get; set; }
        public bool IsMine { get; set; }
        public float Weight { get; set; } = 1.02f;
    }

    public class Landing
    {
        public IEnumerable<PartOfPassenger> Passengers { get; set; }
        public int Floor { get; set; }
    }

    public class PartOfPassenger
    {
        public int Tick { get; set; }
        public int DestFloor { get; set; }
        public bool IsMine { get; set; }
        public float Weight { get; set; }
        public int FromFloor { get; set; }

        public PartOfPassenger(Passenger pass)
        {
            Tick = Game.Counter;
            DestFloor = pass.DestFloor;
            IsMine = pass.IsMine();
            Weight = pass.Weight;
            FromFloor = pass.FromFloor;
        }

        public PartOfPassenger(ExpectedPassengerInfo ep, int destFloor)
        {
            Tick = ep.Tick;
            DestFloor = destFloor;
            IsMine = ep.IsMine;
            Weight = ep.Weight;
            FromFloor = ep.Floor;
        }
    }

    public class FloorsWithPassengers
    {
        public int Floor { get; set; }
        public List<Passenger> WaitingPassengers { get; set; }
        public List<ExpectedPassengerInfo> ExpectedPassengers { get; set; }
        public int TimeWhen { get; internal set; }
        public List<ExpectedPassengerInfo> ExpectedsOnCurrent { get; internal set; }
    }

    public class PassengersWithCostForElevators
    {
        int _floor;
        int _dest;

        public int Floor { get; set; }
        public int Dest { get; set; }

        public PassengersWithCostForElevators(IEnumerable<Passenger> passengers)
        {
            if (passengers.GetDestFloors().Count > 1)
                throw new InvalidOperationException("Переданы пассажиры с разными этажами назначения");
            if (new SortedSet<int>(passengers.Select(p=>p.Floor)).Count > 1)
                throw new InvalidOperationException("Переданы пассажиры с разными этажами");

            var first = passengers.FirstOrDefault();

            Passengers.AddRange(passengers);
            Floor = first?.Floor ?? 0;
            Dest = first?.DestFloor ?? 0;

            var elevsOnFloor = Game.CurrentMyElevators.Where(elev => elev.IsFilling() && elev.Floor == Floor && !Game.GoingOnElevators[elev]);
            //strategy.WriteLog($"elevsOnFloor: {elevsOnFloor.Count()}");
            foreach(var elev in elevsOnFloor)
            {
                var cWay = new Way(elev);
                var landing = new Landing
                {
                    Floor = Floor,
                    Passengers = passengers.ToPart(),
                };
                var wWay = new Way(elev, new Landing[] { landing });

                var incr = new ElevatorsIncreaseCost
                {
                    Elevator = elev,
                    CostWithout = cWay.GetCostPerTick(),
                    CostWith = wWay.GetCostPerTick(),
                };
                IncreasedCosts.Add(incr);
            }

            IncreasedCosts = IncreasedCosts.OrderByDescending(ic => ic.CostWith - ic.CostWithout).ToList();
        }
        public List<Passenger> Passengers { get; } = new List<Passenger>();
        public List<ElevatorsIncreaseCost> IncreasedCosts { get; } = new List<ElevatorsIncreaseCost>();
    }

    public class ElevatorsIncreaseCost
    {
        public Elevator Elevator { get; set; }
        public float CostWithout { get; set; }
        public float CostWith { get; set; }
        //public float IncreasedCost { get; set; }
    }

    public class GameRules
    {
        /// <summary>
        /// Количество этажей
        /// </summary>
        public int FloorsCount { get; set; } = 9;

        /// <summary>
        /// Расстояние от центра этажа до ближайшего лифта на одной стороне
        /// </summary>
        public int FromMidToElvesDistance { get; set; } = 60;

        /// <summary>
        /// Расстояние от центра этажа до места ожидания лифтов
        /// </summary>
        public int FromMidToWaitingDistance { get; set; } = 20;

        /// <summary>
        /// Расстояние между лифтами
        /// </summary>
        public int BetweenElvesDistance { get; set; } = 80;

        /// <summary>
        /// Минимальное время нахождения лифта на этаже
        /// </summary>
        public int MinElevsStandingTime { get; set; } = 40;

        /// <summary>
        /// Минимальное время нахождения лифта на этаже для посадки вражеского пассажира
        /// </summary>
        public int MinStandingTimeForPickupEnemy { get; set; } = 40;

        /// <summary>
        /// Время открытия дверей
        /// </summary>
        public int OpeningDoorsTime { get; set; } = 100;

        /// <summary>
        /// Время закрытия дверей
        /// </summary>
        public int ClosingDoorsTime { get; set; } = 100;

        /// <summary>
        /// Вместимость лифтов
        /// </summary>
        public int ElevsCapacity { get; set; } = 20;

        /// <summary>
        /// Количество пассажиров в лифте, после которого начинается перегрузка
        /// </summary>
        public int ElevsOverloadValue { get; set; } = 10;

        /// <summary>
        /// Интервал появления пассажиров в начале игры
        /// </summary>
        public int PassCreatingInterval { get; set; } = 20;

        /// <summary>
        /// Время создания последней пары пассажиров
        /// </summary>
        public int LastPassCreatingTime { get; set; } = 2000;

        /// <summary>
        /// Время, которое пассажир ждет лифт до ухода на лестницу
        /// </summary>
        public int PassWaitingTime { get; set; } = 500;

        /// <summary>
        /// Время подъема по лестнице на 1 этаж
        /// </summary>
        public int PassUppingTime { get; set; } = 200;

        /// <summary>
        /// Время спуска по лестнице на 1 этаж
        /// </summary>
        public int PassDowningTime { get; set; } = 100;

        /// <summary>
        /// Скорость движение пассажира по этажу
        /// </summary>
        public int PassMovingSpeed { get; set; } = 2;

        /// <summary>
        /// Время, которое пассажир идет на этаж после выхода из лифта
        /// </summary>
        public int AfterLeavingMovingTime { get; set; } = 40;

        /// <summary>
        /// Время, которое пассажир находится на этаже
        /// </summary>
        public int PassWalkingTime { get; set; } = 500;

        /// <summary>
        /// Очки за доставку своего пассажира за каждый этаж
        /// </summary>
        public int CostPerFloorForMy { get; set; } = 10;

        /// <summary>
        /// Очки за доставку чужого пассажира за каждый этаж
        /// </summary>
        public int CostPerFloorForEnemy { get; set; } = 20;

        /// <summary>
        /// Минимальное время подъема лифта на один этаж
        /// </summary>
        public float MinElevsUppingTime { get; set; } = 50.0f;

        /// <summary>
        /// время спуска лифта на один этаж
        /// </summary>
        public float ElevsDowningTime { get; set; } = 50.0f;

        /// <summary>
        /// Множитель для вычисления скорости лифта при перегрузке
        /// </summary>
        public float OverloadMultiplier { get; set; } = 1.1f;
    }

    public enum PassStates : int
    {
        WaitingForElevator = 1,
        MovingToElevator = 2,
        Returning = 3,
        MovingToFloor = 4,
        UsingElevator = 5,
        Exiting = 6,
    }

    public enum ElevStates : int
    {
        Waiting = 0,
        Moving = 1,
        Opening = 2,
        Filling = 3,
        Closing = 4,
    }

    public enum Direction
    {
        Up,
        Down,
        Unknown,
    }
}
