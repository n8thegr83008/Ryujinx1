using Ryujinx.HLE.HOS.Kernel.Common;

namespace Ryujinx.HLE.HOS.Kernel.Memory
{
    class KMemoryRegionManager
    {
        private readonly KPageHeap _pageHeap;

        public ulong Address { get; }
        public ulong Size { get; }

        public KMemoryRegionManager(ulong address, ulong size, ulong endAddr)
        {
            Address = address;
            Size = size;

            _pageHeap = new KPageHeap(address, size);
            _pageHeap.Free(address, size / KMemoryManager.PageSize);
            _pageHeap.UpdateUsedSize();
        }

        public KernelResult AllocatePages(ulong pagesCount, bool backwards, out KPageList pageList)
        {
            if (pagesCount == 0)
            {
                pageList = new KPageList();

                return KernelResult.Success;
            }

            lock (_pageHeap)
            {
                return AllocatePagesImpl(pagesCount, backwards, out pageList);
            }
        }

        public ulong AllocatePagesContiguous(ulong pagesCount, bool backwards)
        {
            if (pagesCount == 0)
            {
                return 0;
            }

            lock (_pageHeap)
            {
                return AllocatePagesContiguousImpl(pagesCount, 1, backwards);
            }
        }

        private KernelResult AllocatePagesImpl(ulong pagesCount, bool backwards, out KPageList pageList)
        {
            pageList = new KPageList();

            int heapIndex = KPageHeap.GetBlockIndex(pagesCount);

            if (heapIndex < 0)
            {
                return KernelResult.OutOfMemory;
            }

            for (int index = heapIndex; index >= 0; index--)
            {
                ulong pagesPerAlloc = KPageHeap.GetBlockPagesCount(index);

                while (pagesCount >= pagesPerAlloc)
                {
                    ulong allocatedBlock = _pageHeap.AllocateBlock(index, true);

                    if (allocatedBlock == 0)
                    {
                        break;
                    }

                    KernelResult result = pageList.AddRange(allocatedBlock, pagesPerAlloc);

                    if (result != KernelResult.Success)
                    {
                        FreePages(pageList);
                        _pageHeap.Free(allocatedBlock, pagesPerAlloc);

                        return result;
                    }

                    pagesCount -= pagesPerAlloc;
                }
            }

            if (pagesCount != 0)
            {
                FreePages(pageList);

                return KernelResult.OutOfMemory;
            }

            return KernelResult.Success;
        }

        private ulong AllocatePagesContiguousImpl(ulong pagesCount, ulong alignPages, bool backwards)
        {
            int heapIndex = KPageHeap.GetAlignedBlockIndex(pagesCount, alignPages);

            ulong allocatedBlock = _pageHeap.AllocateBlock(heapIndex, true);

            if (allocatedBlock == 0)
            {
                return 0;
            }

            ulong allocatedPages = KPageHeap.GetBlockPagesCount(heapIndex);

            if (allocatedPages > pagesCount)
            {
                _pageHeap.Free(allocatedBlock + pagesCount * KMemoryManager.PageSize, allocatedPages - pagesCount);
            }

            return allocatedBlock;
        }

        public void FreePage(ulong address)
        {
            lock (_pageHeap)
            {
                _pageHeap.Free(address, 1);
            }
        }

        public void FreePages(KPageList pageList)
        {
            lock (_pageHeap)
            {
                foreach (KPageNode pageNode in pageList)
                {
                    _pageHeap.Free(pageNode.Address, pageNode.PagesCount);
                }
            }
        }

        public ulong GetFreePages()
        {
            lock (_pageHeap)
            {
                return _pageHeap.GetFreePagesCount();
            }
        }
    }
}