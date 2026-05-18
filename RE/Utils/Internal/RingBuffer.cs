namespace RE.Utils
{
    internal class RingBuffer<T>(int size)
    {
        private readonly T[] _data = new T[size];
        private int _index;

        public int Length => _data.Length;

        public void Add(T item)
        {
            _data[_index] = item;
            _index = (_index + 1) % _data.Length;
        }
        public T[] ToArrayOrdered()
        {
            var res = new T[_data.Length];
            int p = 0;

            for (int i = _index; i < _data.Length; i++)
                res[p++] = _data[i];

            for (int i = 0; i < _index; i++)
                res[p++] = _data[i];
            return res;
        }

        public T[] GetAll() => _data;

        public T Last()
        {
            return ToArrayOrdered().Last();
        }
    }
}
