using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace YoutubeAPI
{
    public class AsyncCacheStream : Stream
    {

        private SpinLock sl = new SpinLock();
        public void Lock()
        {
            Boolean gotLock = false;
            sl.Enter(ref gotLock);
            while (!gotLock)    
            {
                Thread.Sleep(25);
                sl.Enter(ref gotLock);
            }
        }
        public async Task LockAsync()
        {
            Boolean gotLock = false;
            sl.Enter(ref gotLock);
            while (!gotLock)
            {
                await Task.Delay(25);
                sl.Enter(ref gotLock);
            }
        }
        public void ReleaseLock()
        {
            sl.Exit();
        }

        public bool isWriting { get; private set; }

        private MemoryStream data = new();

        public override bool CanRead => false;

        public override bool CanSeek => data.CanSeek;

        public override bool CanWrite => data.CanWrite;

        public override long Length => data.Length;

        public override long Position { get => data.Position; set => data.Position = value; }

        public override void Flush() => data.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => data.Seek(offset, origin);

        public override void SetLength(long value) => data.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            Lock();
            try
            {
                data.Write(buffer, offset, count);
            }
            finally
            {
                ReleaseLock();
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await LockAsync();
            try
            {
                await data.WriteAsync(buffer, offset, count);
            }
            finally
            {
                ReleaseLock();
            }
        }

        public Stream CreateReader() => new AsyncCacheStreamReader(this);

        public void StartWriting()
        {
            isWriting = true;
        }

        public void FinishWriting()
        {
            isWriting = false;
        }

        private class AsyncCacheStreamReader : Stream
        {
            private readonly AsyncCacheStream stream;

            public AsyncCacheStreamReader(AsyncCacheStream stream)
            {
                this.stream = stream;
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => stream.data.Length;

            public override long Position { get; set; } = 0;

            public override void Flush() => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                stream.Lock();
                Int64 pos = stream.data.Position;
                Int32 ret = -1;
                try
                {
                    stream.data.Position = this.Position;
                    ret = stream.data.Read(buffer, offset, count);
                    this.Position += ret;
                } finally
                {
                    stream.data.Position = pos;
                    stream.ReleaseLock();
                }
                return ret;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await stream.LockAsync();
                Int64 pos = stream.data.Position;
                Int32 ret = -1;
                try
                {
                    stream.data.Position = this.Position;
                    ret = await stream.data.ReadAsync(buffer, offset, count);
                    this.Position += ret;
                }
                finally
                {
                    stream.data.Position = pos;
                    stream.ReleaseLock();
                }
                return ret;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        this.Position = offset;
                        break;

                    case SeekOrigin.Current:
                        this.Position += offset;
                        break;

                    case SeekOrigin.End:
                        this.Position = stream.data.Length - offset;
                        break;
                }
                return this.Position;
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}