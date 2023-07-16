using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    // This class keeps an ordered list of objects with a DateTime object 
    // called Time. New objects are added if older than oldest or newer than newest,
    // therefor objects must be added in order. Throws an exception if this is not
    // adhered to.
    // 
    //Assumes FIFO, set FIFO parametar to false to get FILO-behaviour
    public class LimitedDateTimeList<T> where T : ITimeBound
    {
        /// <summary>
        /// Constructs a <see creef="LimitedDateTimeList"/> object.
        /// </summary>
        /// <param name="limit">capacity</param>
        public LimitedDateTimeList(int limit)
        {
            Limit = limit;
            FifoBehaviour = true;
            backwards = false;
            orderedList = new LinkedList<T>();
        }
        public LimitedDateTimeList(bool fifo, int limit)
        {
            Limit = limit;
            FifoBehaviour = fifo;
            backwards = false;
            orderedList = new LinkedList<T>();
        }

        public LimitedDateTimeList(IEnumerable<T> collection, int limit)
        {
            Limit = limit;
            FifoBehaviour = true;
            orderedList = new LinkedList<T>(collection);

            CheckDirection();

            TrimToSize();
        }

        public LimitedDateTimeList(IEnumerable<T> collection, int limit, bool fifo)
        {
            Limit = limit;
            FifoBehaviour = fifo;
            orderedList = new LinkedList<T>(collection);

            CheckDirection();

            TrimToSize();
        }

        // Properties

        public T Newest
        {
            get
            {
                if (!backwards)
                {
                    if (orderedList.First != null)
                    {
                        return orderedList.First.Value;
                    }
                    else
                    {
                        return default(T);
                    }
                }
                else
                {
                    if (orderedList.Last != null)
                    {
                        return orderedList.Last.Value;
                    }
                    else
                    {
                        return default(T);
                    }
                }
            }
        }
        public T Oldest
        {
            get
            {
                if (!backwards)
                {
                    if (orderedList.Last != null)
                    {
                        return orderedList.Last.Value;
                    }
                    else
                    {
                        return default(T);
                    }
                }
                else
                {
                    if (orderedList.First != null)
                    {
                        return orderedList.First.Value;
                    }
                    else
                    {
                        return default(T);
                    }
                }
            }
        }
        public int Limit { get; set; }
        public bool FifoBehaviour { get; }
        public int Count
        {
            get
            {
                return orderedList.Count;
            }
        }
        public LinkedList<T> InternalList
        {
            get
            {
                return orderedList;
            }
        }

        // Methods
        public void AddValue(T value)
        {
            try
            {
                if (orderedList.Count == 0)
                {
                    if (!backwards)
                    {
                        orderedList.AddFirst(value);
                    }
                    else
                    {
                        orderedList.AddLast(value);
                    }
                }
                else if (orderedList.Count < Limit)
                {
                    if (!backwards)
                    {
                        if (value.Time > orderedList.First.Value.Time)
                        {
                            orderedList.AddFirst(value);
                        }
                        else if (value.Time < orderedList.Last.Value.Time)
                        {
                            orderedList.AddLast(value);
                        }
                        else
                        {
                            throw new Exception("Value inbetween");
                        }
                    }
                    else
                    {
                        if (value.Time > orderedList.Last.Value.Time)
                        {
                            orderedList.AddLast(value);
                        }
                        else if (value.Time < orderedList.First.Value.Time)
                        {
                            orderedList.AddFirst(value);
                        }
                        else
                        {
                            throw new Exception("Value inbetween");
                        }
                    }

                }
                else if (orderedList.Count == Limit)
                {
                    if (!backwards)
                    {
                        if (value.Time>orderedList.First.Value.Time)
                        {
                            orderedList.AddFirst(value);
                        }
                        else if (value.Time<orderedList.Last.Value.Time)
                        {
                            orderedList.AddLast(value);
                        }
                        else
                        {
                            throw new Exception("Value in between");
                        }
                    }
                    else
                    {
                        if (value.Time > orderedList.Last.Value.Time)
                        {
                            orderedList.AddLast(value);
                        }
                        else if (value.Time < orderedList.First.Value.Time)
                        {
                            orderedList.AddFirst(value);
                        }
                        else
                        {
                            throw new Exception("Value in between");
                        }
                    }
                    TrimToSize();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        public T GetRemoveNewest()
        {
            try
            {
                T returnValue;
                if (!backwards)
                {
                    returnValue = orderedList.First.Value;
                    orderedList.RemoveFirst();
                }
                else
                {
                    returnValue = orderedList.Last.Value;
                    orderedList.RemoveLast();
                }
                return returnValue;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return default(T);
            }
            
        }
        public T GetRemoveOldest()
        {
            try
            {
                T returnValue;
                if (!backwards)
                {
                    returnValue = orderedList.Last.Value;
                    orderedList.RemoveLast();
                }
                else
                {
                    returnValue = orderedList.First.Value;
                    orderedList.RemoveFirst();
                }
                return returnValue;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return default(T);
            }
        }
        public bool CheckIntegrity()
        {
            try
            {
                LinkedListNode<T> node;
                if (orderedList.Count > 1)
                {
                    if (!backwards)
                    {
                        node = orderedList.First;
                        for (int i = 0; i < orderedList.Count; i++)
                        {
                            if (node.Previous != null)
                            {
                                if (node.Value.Time > node.Previous.Value.Time)
                                {
                                    return false;
                                }
                            }
                            node = node.Next;
                        }
                    }
                    else
                    {
                        node = orderedList.Last;
                        for (int i = 0; i < orderedList.Count; i++)
                        {
                            if (node.Next != null)
                            {
                                if (node.Value.Time > node.Next.Value.Time)
                                {
                                    return false;
                                }
                            }
                            node = node.Previous;
                        }
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return false;
            }
            
        }

        private void TrimToSize()
        {
            try
            {
                if (orderedList.Count > Limit)
                {
                    while (orderedList.Count > Limit)
                    {
                        if (!backwards)
                        {
                            if (FifoBehaviour)
                            {
                                orderedList.RemoveLast();
                            }
                            else
                            {
                                orderedList.RemoveFirst();
                            }
                        }
                        else
                        {
                            if (FifoBehaviour)
                            {
                                orderedList.RemoveFirst();
                            }
                            else
                            {
                                orderedList.RemoveLast();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        //check direction of element order if >2 elements
        private void CheckDirection()
        {
            try
            {
                if (orderedList.Count > 1)
                {
                    if (orderedList.First.Value.Time > orderedList.Last.Value.Time)
                    {
                        backwards = false;
                    }
                    else
                    {
                        backwards = true;
                    }
                }
                else
                {
                    backwards = false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        // Backing field
        private LinkedList<T> orderedList;
        private bool backwards;
    }
}
