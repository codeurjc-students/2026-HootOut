import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { h } from 'vue'
import InfiniteScroll from '@/components/shared/infiniteScroll/InfiniteScrollList.vue'

// ---------- IntersectionObserver fake (jsdom does not implement it) ----------

class FakeIntersectionObserver {
  static instances: FakeIntersectionObserver[] = []

  callback: IntersectionObserverCallback
  options?: IntersectionObserverInit
  observe = vi.fn()
  unobserve = vi.fn()
  disconnect = vi.fn()

  constructor(callback: IntersectionObserverCallback, options?: IntersectionObserverInit) {
    this.callback = callback
    this.options = options
    FakeIntersectionObserver.instances.push(this)
  }

  // Simulates the browser notifying an intersection change of the observed element
  async trigger(intersectionRatio = 1) {
    const entries = [{ intersectionRatio } as IntersectionObserverEntry]
    await this.callback(entries, this as unknown as IntersectionObserver)
    await flushPromises()
  }

  async triggerWith(entries: IntersectionObserverEntry[]) {
    await this.callback(entries, this as unknown as IntersectionObserver)
    await flushPromises()
  }
}

// ---------- helpers ----------

type Item = { id: number; name: string }

const loadMore = vi.fn<(nextPage: string | null) => Promise<string | null | undefined>>()

function makeItems(count: number, offset = 0): Item[] {
  return Array.from({ length: count }, (_, i) => ({
    id: offset + i + 1,
    name: `item-${offset + i + 1}`,
  }))
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

function mountScroll(items: Item[] = makeItems(3)) {
  return mount(InfiniteScroll, {
    props: { items, loadMore },
    slots: {
      'scroll-item': ({ item }: { item: unknown }) =>
        h('p', { class: 'scroll-item' }, (item as Item).name),
    },
  })
}

const observer = () => FakeIntersectionObserver.instances[0]!
const loader = (wrapper: VueWrapper) => wrapper.find('.mx-auto')
const renderedItems = (wrapper: VueWrapper) => wrapper.findAll('.scroll-item')

// ---------- tests ----------

describe('InfiniteScroll.vue', () => {
  beforeEach(() => {
    FakeIntersectionObserver.instances = []
    vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver)
    loadMore.mockReset()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  describe('rendering', () => {
    it('renders the scroll-item slot once per item, passing the item', () => {
      const wrapper = mountScroll(makeItems(3))

      expect(renderedItems(wrapper).map((item) => item.text())).toEqual([
        'item-1',
        'item-2',
        'item-3',
      ])
    })

    it('renders no items but shows the loader when the list is empty', () => {
      const wrapper = mountScroll([])

      expect(renderedItems(wrapper)).toHaveLength(0)
      expect(loader(wrapper).exists()).toBe(true)
    })

    it('renders the new items when the items prop changes', async () => {
      const wrapper = mountScroll(makeItems(2))

      await wrapper.setProps({ items: makeItems(4) })

      expect(renderedItems(wrapper)).toHaveLength(4)
    })

    it('renders the loader after the items', () => {
      const wrapper = mountScroll(makeItems(2))

      const children = Array.from(wrapper.element.children)
      expect(children.at(-1)).toBe(loader(wrapper).element)
    })
  })

  describe('observer setup', () => {
    it('creates the observer with a 0.5 threshold', () => {
      mountScroll()

      expect(FakeIntersectionObserver.instances).toHaveLength(1)
      expect(observer().options).toEqual({ threshold: 0.5 })
    })

    it('observes the loader element on mount', () => {
      const wrapper = mountScroll()

      expect(observer().observe).toHaveBeenCalledTimes(1)
      expect(observer().observe).toHaveBeenCalledWith(loader(wrapper).element)
    })

    it('does not call loadMore until the loader becomes visible', async () => {
      mountScroll()
      await flushPromises()

      expect(loadMore).not.toHaveBeenCalled()
    })
  })

  describe('loading pages', () => {
    it('calls loadMore with null the first time the loader becomes visible', async () => {
      loadMore.mockResolvedValueOnce('cursor-2')
      mountScroll()

      await observer().trigger()

      expect(loadMore).toHaveBeenCalledTimes(1)
      expect(loadMore).toHaveBeenCalledWith(null)
    })

    it('passes the cursor returned by the previous load to the next one', async () => {
      loadMore
        .mockResolvedValueOnce('cursor-2')
        .mockResolvedValueOnce('cursor-3')
        .mockResolvedValueOnce('cursor-4')
      mountScroll()

      await observer().trigger()
      await observer().trigger()
      await observer().trigger()

      expect(loadMore.mock.calls.map(([cursor]) => cursor)).toEqual([null, 'cursor-2', 'cursor-3'])
    })

    it('keeps the loader while loadMore returns a cursor', async () => {
      loadMore.mockResolvedValueOnce('cursor-2')
      const wrapper = mountScroll()

      await observer().trigger()

      expect(loader(wrapper).exists()).toBe(true)
    })

    it('ignores entries that are not intersecting', async () => {
      mountScroll()

      await observer().trigger(0)

      expect(loadMore).not.toHaveBeenCalled()
    })

    it('ignores an empty list of entries', async () => {
      mountScroll()

      await observer().triggerWith([])

      expect(loadMore).not.toHaveBeenCalled()
    })
  })

  describe('no more pages', () => {
    it.each([null, undefined])('hides the loader when loadMore returns %s', async (lastCursor) => {
      loadMore.mockResolvedValueOnce('cursor-2').mockResolvedValueOnce(lastCursor)
      const wrapper = mountScroll()

      await observer().trigger()
      await observer().trigger()

      expect(loader(wrapper).exists()).toBe(false)
    })

    it('hides the loader when the first load already has no more pages', async () => {
      loadMore.mockResolvedValueOnce(null)
      const wrapper = mountScroll()

      await observer().trigger()

      expect(loader(wrapper).exists()).toBe(false)
    })

    it('keeps rendering the items after the last page', async () => {
      loadMore.mockResolvedValueOnce(null)
      const wrapper = mountScroll(makeItems(3))

      await observer().trigger()

      expect(renderedItems(wrapper)).toHaveLength(3)
    })

    it('disconnects the observer when there are no more pages', async () => {
      loadMore.mockResolvedValueOnce(null)
      mountScroll()

      await observer().trigger()

      expect(observer().disconnect).toHaveBeenCalledTimes(1)
    })

    it('does not call loadMore again after the last page', async () => {
      loadMore.mockResolvedValueOnce(null)
      mountScroll()

      await observer().trigger()
      await observer().trigger() // late notification from the browser

      expect(loadMore).toHaveBeenCalledTimes(1)
    })
  })

  describe('concurrent triggers', () => {
    it('does not call loadMore again while a load is in progress', async () => {
      const pending = deferred<string | null>()
      loadMore.mockReturnValueOnce(pending.promise)
      mountScroll()

      void observer().trigger()
      void observer().trigger()
      await flushPromises()

      expect(loadMore).toHaveBeenCalledTimes(1)

      pending.resolve('cursor-2')
      await flushPromises()
    })

    it('loads the next page once the pending load has finished', async () => {
      const pending = deferred<string | null>()
      loadMore.mockReturnValueOnce(pending.promise).mockResolvedValueOnce('cursor-3')
      mountScroll()

      void observer().trigger()
      pending.resolve('cursor-2')
      await flushPromises()
      await observer().trigger()

      expect(loadMore).toHaveBeenCalledTimes(2)
      expect(loadMore).toHaveBeenLastCalledWith('cursor-2')
    })
  })

  describe('errors', () => {
    it('keeps the loader when loadMore fails', async () => {
      loadMore.mockRejectedValueOnce(new Error('500'))
      const wrapper = mountScroll()

      await observer().trigger()

      expect(loader(wrapper).exists()).toBe(true)
    })

    it('retries with the same cursor the next time the loader becomes visible', async () => {
      loadMore
        .mockResolvedValueOnce('cursor-2')
        .mockRejectedValueOnce(new Error('500'))
        .mockResolvedValueOnce('cursor-3')
      mountScroll()

      await observer().trigger() // loads page 1
      await observer().trigger() // page 2 fails
      await observer().trigger() // retries page 2

      expect(loadMore.mock.calls.map(([cursor]) => cursor)).toEqual([null, 'cursor-2', 'cursor-2'])
    })

    it('retries the first page after a failed first load', async () => {
      loadMore.mockRejectedValueOnce(new Error('500')).mockResolvedValueOnce('cursor-2')
      mountScroll()

      await observer().trigger()
      await observer().trigger()

      expect(loadMore.mock.calls.map(([cursor]) => cursor)).toEqual([null, null])
    })

    it('does not restart the observer after a failure, to avoid a retry loop', async () => {
      loadMore.mockRejectedValueOnce(new Error('500'))
      mountScroll()

      await observer().trigger()

      expect(observer().unobserve).not.toHaveBeenCalled()
      expect(observer().observe).toHaveBeenCalledTimes(1) // only the initial one
    })

    it('does not disconnect the observer after a failure', async () => {
      loadMore.mockRejectedValueOnce(new Error('500'))
      mountScroll()

      await observer().trigger()

      expect(observer().disconnect).not.toHaveBeenCalled()
    })
  })

  describe('restarting the observer', () => {
    it('observes the loader again after a successful load', async () => {
      loadMore.mockResolvedValueOnce('cursor-2')
      const wrapper = mountScroll()

      await observer().trigger()

      const element = loader(wrapper).element
      expect(observer().unobserve).toHaveBeenCalledWith(element)
      expect(observer().observe).toHaveBeenCalledTimes(2)
      expect(observer().observe).toHaveBeenLastCalledWith(element)
    })

    it('restarts the observer after the new items have been rendered', async () => {
      const wrapper = mountScroll(makeItems(2))
      let itemsWhenObserved = -1
      observer().observe.mockImplementation(() => {
        itemsWhenObserved = renderedItems(wrapper).length
      })
      // the parent adds the new items before resolving loadMore
      loadMore.mockImplementationOnce(async () => {
        await wrapper.setProps({ items: makeItems(4) })
        return 'cursor-2'
      })

      await observer().trigger()

      expect(itemsWhenObserved).toBe(4)
    })

    it('does not restart the observer after the last page', async () => {
      loadMore.mockResolvedValueOnce(null)
      mountScroll()

      await observer().trigger()

      expect(observer().unobserve).not.toHaveBeenCalled()
      expect(observer().observe).toHaveBeenCalledTimes(1)
    })
  })

  describe('unmount', () => {
    it('disconnects the observer on unmount', () => {
      const wrapper = mountScroll()

      wrapper.unmount()

      expect(observer().disconnect).toHaveBeenCalledTimes(1)
    })

    it('disconnects the observer on unmount even when the loader is already hidden', async () => {
      loadMore.mockResolvedValueOnce(null)
      const wrapper = mountScroll()
      await observer().trigger()
      observer().disconnect.mockClear()

      wrapper.unmount()

      expect(observer().disconnect).toHaveBeenCalledTimes(1)
    })
  })
})
