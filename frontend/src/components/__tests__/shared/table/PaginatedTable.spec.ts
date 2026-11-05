import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { h, nextTick } from 'vue'
import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
import type { PageRequest, PageResult, TableHeader } from '@/types'

type Item = { id: number; name: string; createdAt: string }

const headers: TableHeader[] = [
  { key: 'id', name: 'ID', canSort: true },
  { key: 'name', name: 'Name', canSort: true, default: true },
  { key: 'createdAt', name: 'Created', canSort: true },
  { key: 'status', name: 'Status', canSort: false },
]

const queryFunction = vi.fn<(request: PageRequest) => Promise<PageResult<Item> | null>>()

// ---------- helpers ----------

function makeItems(count: number, offset = 0): Item[] {
  return Array.from({ length: count }, (_, i) => ({
    id: offset + i + 1,
    name: `user-${offset + i + 1}`,
    createdAt: '2026-01-01',
  }))
}

function makePage(
  items: Item[],
  options: { next?: string | null; total?: number; pageSize?: number } = {}
): PageResult<Item> {
  return {
    items,
    next: options.next ?? null,
    total: options.total ?? items.length,
    pageSize: options.pageSize ?? 10,
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((res) => {
    resolve = res
  })
  return { promise, resolve }
}

async function mountTable(
  props: Partial<{ headers: TableHeader[]; hasActions: boolean }> = {},
  slots = {}
) {
  const wrapper = mount(PaginatedTable, {
    props: { headers, queryFunction, ...props },
    slots,
  })
  await flushPromises()
  return wrapper
}

const rows = (wrapper: VueWrapper) => wrapper.findAll('tbody tr')
const headerCell = (wrapper: VueWrapper, name: string) =>
  wrapper.findAll('th').find((th) => th.text().includes(name))!
const nextButton = (wrapper: VueWrapper) =>
  wrapper.findAll('button').find((b) => b.text() === 'chevron_right')
const prevButton = (wrapper: VueWrapper) =>
  wrapper.findAll('button').find((b) => b.text() === 'chevron_left')
const lastRequest = () => queryFunction.mock.lastCall?.[0]

async function clickNext(wrapper: VueWrapper) {
  await nextButton(wrapper)!.trigger('click')
  await flushPromises()
}

async function clickPrev(wrapper: VueWrapper) {
  await prevButton(wrapper)!.trigger('click')
  await flushPromises()
}

async function clickHeader(wrapper: VueWrapper, name: string) {
  await headerCell(wrapper, name).trigger('click')
  await flushPromises()
}

// ---------- tests ----------

describe('PaginatedTable.vue', () => {
  beforeEach(() => {
    queryFunction.mockReset()
  })

  describe('initial load', () => {
    it('requests the first page on mount with the default sort column', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(3)))

      await mountTable()

      expect(queryFunction).toHaveBeenCalledTimes(1)
      expect(lastRequest()).toEqual({ next: null, descending: true, pageSize: 10, sortBy: 'name' })
    })

    it('uses the first header as sort column when no header is marked as default', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(3)))

      await mountTable({ headers: headers.map((h) => ({ ...h, default: false })) })

      expect(lastRequest()?.sortBy).toBe('id')
    })

    it('renders a header cell for each header', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(3)))

      const wrapper = await mountTable()

      expect(wrapper.findAll('th').map((th) => th.text())).toEqual(
        expect.arrayContaining([
          expect.stringContaining('ID'),
          expect.stringContaining('Name'),
          expect.stringContaining('Created'),
          expect.stringContaining('Status'),
        ])
      )
      expect(wrapper.findAll('th')).toHaveLength(headers.length)
    })

    it('renders one row per item and one cell per header', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(3)))

      const wrapper = await mountTable()

      expect(rows(wrapper)).toHaveLength(3)
      expect(rows(wrapper)[0]!.findAll('td')).toHaveLength(headers.length)
      expect(rows(wrapper)[0]!.text()).toContain('user-1')
      expect(rows(wrapper)[2]!.text()).toContain('user-3')
    })
  })

  describe('empty table', () => {
    it('renders no rows when there are no items', async () => {
      queryFunction.mockResolvedValueOnce(makePage([], { total: 0 }))

      const wrapper = await mountTable()

      expect(rows(wrapper)).toHaveLength(0)
    })

    it('shows "Page 1 / 1" and no navigation buttons', async () => {
      queryFunction.mockResolvedValueOnce(makePage([], { total: 0 }))

      const wrapper = await mountTable()

      expect(wrapper.text()).toContain('Page 1 / 1')
      expect(nextButton(wrapper)).toBeUndefined()
      expect(prevButton(wrapper)).toBeUndefined()
    })

    it('still renders the headers', async () => {
      queryFunction.mockResolvedValueOnce(makePage([], { total: 0 }))

      const wrapper = await mountTable()

      expect(wrapper.findAll('th')).toHaveLength(headers.length)
    })
  })

  describe('pagination', () => {
    it('shows only the next button on the first page when there are more pages', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))

      const wrapper = await mountTable()

      expect(nextButton(wrapper)).toBeDefined()
      expect(prevButton(wrapper)).toBeUndefined()
      expect(wrapper.text()).toContain('Page 1 / 3')
    })

    it('shows no navigation buttons when everything fits in one page', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(7), { total: 7 }))

      const wrapper = await mountTable()

      expect(nextButton(wrapper)).toBeUndefined()
      expect(prevButton(wrapper)).toBeUndefined()
      expect(wrapper.text()).toContain('Page 1 / 1')
    })

    it('goes to the next page using the cursor returned by the previous page', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))

      const wrapper = await mountTable()
      await clickNext(wrapper)

      expect(lastRequest()?.next).toBe('cursor-2')
      expect(wrapper.text()).toContain('Page 2 / 3')
      expect(rows(wrapper)[0]!.text()).toContain('user-11')
      expect(prevButton(wrapper)).toBeDefined()
      expect(nextButton(wrapper)).toBeDefined()
    })

    it('hides the next button on the last page', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 15 }))
        .mockResolvedValueOnce(makePage(makeItems(5, 10), { next: null, total: 15 }))

      const wrapper = await mountTable()
      await clickNext(wrapper)

      expect(wrapper.text()).toContain('Page 2 / 2')
      expect(rows(wrapper)).toHaveLength(5)
      expect(nextButton(wrapper)).toBeUndefined()
      expect(prevButton(wrapper)).toBeDefined()
    })

    it('goes back to the previous page reusing its cursor', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))

      const wrapper = await mountTable()
      await clickNext(wrapper)
      await clickPrev(wrapper)

      expect(lastRequest()?.next).toBeNull()
      expect(wrapper.text()).toContain('Page 1 / 3')
      expect(rows(wrapper)[0]!.text()).toContain('user-1')
      expect(prevButton(wrapper)).toBeUndefined()
    })

    it('navigates forward and backward through several pages with the right cursors', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(5, 20), { next: null, total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(5, 20), { next: null, total: 25 }))

      const wrapper = await mountTable()

      await clickNext(wrapper) // page 2
      await clickNext(wrapper) // page 3
      expect(wrapper.text()).toContain('Page 3 / 3')
      expect(nextButton(wrapper)).toBeUndefined()

      await clickPrev(wrapper) // page 2
      expect(wrapper.text()).toContain('Page 2 / 3')

      await clickNext(wrapper) // page 3 again
      expect(wrapper.text()).toContain('Page 3 / 3')

      expect(queryFunction.mock.calls.map(([request]) => request.next)).toEqual([
        null,
        'cursor-2',
        'cursor-3',
        'cursor-2',
        'cursor-3',
      ])
    })

    it('always requests pages of 10 items keeping the current sort', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))

      const wrapper = await mountTable()
      await clickNext(wrapper)

      expect(lastRequest()).toMatchObject({ pageSize: 10, sortBy: 'name', descending: true })
    })
  })

  describe('total pages', () => {
    it.each([
      { total: 0, expected: 1 },
      { total: 1, expected: 1 },
      { total: 10, expected: 1 },
      { total: 11, expected: 2 },
      { total: 20, expected: 2 },
      { total: 25, expected: 3 },
    ])('shows $expected total pages when there are $total items', async ({ total, expected }) => {
      queryFunction.mockResolvedValueOnce(
        makePage(makeItems(Math.min(total, 10)), { total, next: total > 10 ? 'cursor-2' : null })
      )

      const wrapper = await mountTable()

      expect(wrapper.text()).toContain(`Page 1 / ${expected}`)
    })
  })

  describe('sorting', () => {
    it('shows the sort arrow only on the current sort column', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(3)))

      const wrapper = await mountTable()

      expect(headerCell(wrapper, 'Name').find('.material-symbols-sharp').exists()).toBe(true)
      expect(headerCell(wrapper, 'ID').find('.material-symbols-sharp').exists()).toBe(false)
    })

    it('shows "keyboard_arrow_up" when sorting descending', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(3)))

      const wrapper = await mountTable()

      expect(headerCell(wrapper, 'Name').find('.material-symbols-sharp').text()).toBe(
        'keyboard_arrow_up'
      )
    })

    it('sorts by a new column in descending order when its header is clicked', async () => {
      queryFunction.mockResolvedValue(makePage(makeItems(3)))

      const wrapper = await mountTable()
      await clickHeader(wrapper, 'ID')

      expect(lastRequest()).toMatchObject({ sortBy: 'id', descending: true, next: null })
      expect(headerCell(wrapper, 'ID').find('.material-symbols-sharp').exists()).toBe(true)
      expect(headerCell(wrapper, 'Name').find('.material-symbols-sharp').exists()).toBe(false)
    })

    it('toggles the direction when the current sort column is clicked again', async () => {
      queryFunction.mockResolvedValue(makePage(makeItems(3)))

      const wrapper = await mountTable()

      await clickHeader(wrapper, 'Name')
      expect(lastRequest()).toMatchObject({ sortBy: 'name', descending: false })
      expect(headerCell(wrapper, 'Name').find('.material-symbols-sharp').text()).toBe(
        'keyboard_arrow_down'
      )

      await clickHeader(wrapper, 'Name')
      expect(lastRequest()).toMatchObject({ sortBy: 'name', descending: true })
      expect(headerCell(wrapper, 'Name').find('.material-symbols-sharp').text()).toBe(
        'keyboard_arrow_up'
      )
    })

    it('resets to descending when switching to another column', async () => {
      queryFunction.mockResolvedValue(makePage(makeItems(3)))

      const wrapper = await mountTable()
      await clickHeader(wrapper, 'Name') // name ascending
      await clickHeader(wrapper, 'ID')

      expect(lastRequest()).toMatchObject({ sortBy: 'id', descending: true })
    })

    it('sends the column key in lower case', async () => {
      queryFunction.mockResolvedValue(makePage(makeItems(3)))

      const wrapper = await mountTable()
      await clickHeader(wrapper, 'Created')

      expect(lastRequest()?.sortBy).toBe('createdat')
    })

    it('does nothing when a non sortable header is clicked', async () => {
      queryFunction.mockResolvedValue(makePage(makeItems(3)))

      const wrapper = await mountTable()
      await clickHeader(wrapper, 'Status')

      expect(queryFunction).toHaveBeenCalledTimes(1)
      expect(headerCell(wrapper, 'Status').find('.material-symbols-sharp').exists()).toBe(false)
    })

    it('goes back to the first page when sorting from another page', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2-by-id', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3-by-id', total: 25 }))

      const wrapper = await mountTable()
      await clickNext(wrapper)
      await clickHeader(wrapper, 'ID')

      expect(lastRequest()).toMatchObject({ next: null, sortBy: 'id' })
      expect(wrapper.text()).toContain('Page 1 / 3')
      expect(prevButton(wrapper)).toBeUndefined()

      // the old cursors are discarded: the next page uses the cursor of the new sort
      await clickNext(wrapper)
      expect(lastRequest()?.next).toBe('cursor-2-by-id')
    })
  })

  describe('errors', () => {
    it('stays on the current page when the next page request fails', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockRejectedValueOnce(new Error('500'))

      const wrapper = await mountTable()
      await clickNext(wrapper)

      expect(wrapper.text()).toContain('Page 1 / 3')
      expect(rows(wrapper)[0]!.text()).toContain('user-1')
      expect(nextButton(wrapper)).toBeDefined()
    })

    it('stays on the current page when the request returns no page', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockResolvedValueOnce(null)

      const wrapper = await mountTable()
      await clickNext(wrapper)

      expect(wrapper.text()).toContain('Page 1 / 3')
      expect(rows(wrapper)[0]!.text()).toContain('user-1')
    })

    it('can retry the next page after a failure using the same cursor', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockRejectedValueOnce(new Error('500'))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))

      const wrapper = await mountTable()
      await clickNext(wrapper) // fails
      await clickNext(wrapper) // retry

      expect(lastRequest()?.next).toBe('cursor-2')
      expect(wrapper.text()).toContain('Page 2 / 3')
    })

    it('stays on the current page when the previous page request fails', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))
        .mockRejectedValueOnce(new Error('500'))

      const wrapper = await mountTable()
      await clickNext(wrapper)
      await clickPrev(wrapper)

      expect(wrapper.text()).toContain('Page 2 / 3')
      expect(rows(wrapper)[0]!.text()).toContain('user-11')
    })

    it('keeps the current rows when sorting fails', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(3)))
        .mockRejectedValueOnce(new Error('500'))

      const wrapper = await mountTable()
      await clickHeader(wrapper, 'ID')

      expect(rows(wrapper)).toHaveLength(3)
      expect(rows(wrapper)[0]!.text()).toContain('user-1')
    })

    it('renders an empty table when the initial request fails', async () => {
      queryFunction.mockRejectedValueOnce(new Error('500'))

      const wrapper = await mountTable()

      expect(rows(wrapper)).toHaveLength(0)
      expect(wrapper.text()).toContain('Page 1 / 1')
    })
  })

  describe('loading state', () => {
    it('disables the next button while the next page is loading', async () => {
      const pending = deferred<PageResult<Item>>()
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockReturnValueOnce(pending.promise)

      const wrapper = await mountTable()
      await nextButton(wrapper)!.trigger('click')
      await nextTick()

      expect(nextButton(wrapper)!.attributes('disabled')).toBeDefined()

      pending.resolve(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))
      await flushPromises()

      expect(nextButton(wrapper)!.attributes('disabled')).toBeUndefined()
      expect(prevButton(wrapper)!.attributes('disabled')).toBeUndefined()
    })

    it('disables both buttons while the previous page is loading', async () => {
      const pending = deferred<PageResult<Item>>()
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockResolvedValueOnce(makePage(makeItems(10, 10), { next: 'cursor-3', total: 25 }))
        .mockReturnValueOnce(pending.promise)

      const wrapper = await mountTable()
      await clickNext(wrapper)
      await prevButton(wrapper)!.trigger('click')
      await nextTick()

      expect(prevButton(wrapper)!.attributes('disabled')).toBeDefined()
      expect(nextButton(wrapper)!.attributes('disabled')).toBeDefined()

      pending.resolve(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
      await flushPromises()
    })

    it('disables the buttons while sorting', async () => {
      const pending = deferred<PageResult<Item>>()
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockReturnValueOnce(pending.promise)

      const wrapper = await mountTable()
      await headerCell(wrapper, 'ID').trigger('click')
      await nextTick()

      expect(nextButton(wrapper)!.attributes('disabled')).toBeDefined()

      pending.resolve(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
      await flushPromises()

      expect(nextButton(wrapper)!.attributes('disabled')).toBeUndefined()
    })

    it('enables the buttons again after a failed request', async () => {
      queryFunction
        .mockResolvedValueOnce(makePage(makeItems(10), { next: 'cursor-2', total: 25 }))
        .mockRejectedValueOnce(new Error('500'))

      const wrapper = await mountTable()
      await clickNext(wrapper)

      expect(nextButton(wrapper)!.attributes('disabled')).toBeUndefined()
    })
  })

  describe('mapper', () => {
    it('shows the mapped value in the cell', async () => {
      const mappedHeaders: TableHeader[] = [
        { key: 'id', name: 'ID' },
        { key: 'name', name: 'Name', mapper: (value: string) => value.toUpperCase() },
      ]
      queryFunction.mockResolvedValueOnce(makePage(makeItems(1)))

      const wrapper = await mountTable({ headers: mappedHeaders })

      expect(rows(wrapper)[0]!.findAll('td')[1]!.text()).toBe('USER-1')
    })

    it.each([undefined, null])(
      'shows the original value when the mapper returns %s',
      async (mapped) => {
        const mappedHeaders: TableHeader[] = [{ key: 'name', name: 'Name', mapper: () => mapped }]
        queryFunction.mockResolvedValueOnce(makePage(makeItems(1)))

        const wrapper = await mountTable({ headers: mappedHeaders })

        expect(rows(wrapper)[0]!.findAll('td')[0]!.text()).toBe('user-1')
      }
    )

    it('passes the original value to the mapper', async () => {
      const mapper = vi.fn(() => 'mapped')
      queryFunction.mockResolvedValueOnce(makePage(makeItems(1)))

      await mountTable({ headers: [{ key: 'name', name: 'Name', mapper }] })

      expect(mapper).toHaveBeenCalledWith('user-1')
    })
  })

  describe('actions slot', () => {
    it('does not render the actions column when hasActions is false', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(2)))

      const wrapper = await mountTable(
        { hasActions: false },
        {
          actions: () => h('button', 'Edit'),
        }
      )

      expect(wrapper.findAll('th').some((th) => th.text() === 'Actions')).toBe(false)
      expect(rows(wrapper)[0]!.findAll('td')).toHaveLength(headers.length)
      expect(wrapper.text()).not.toContain('Edit')
    })

    it('renders the actions column and the slot content for each row', async () => {
      queryFunction.mockResolvedValueOnce(makePage(makeItems(2)))

      const wrapper = await mountTable(
        { hasActions: true },
        {
          actions: () => h('button', { class: 'edit-btn' }, 'Edit'),
        }
      )

      expect(wrapper.findAll('th').some((th) => th.text() === 'Actions')).toBe(true)
      expect(rows(wrapper)[0]!.findAll('td')).toHaveLength(headers.length + 1)
      expect(wrapper.findAll('.edit-btn')).toHaveLength(2)
    })

    it('passes the row to the slot with value and display for each header', async () => {
      const actions = vi.fn(({ row }: { row: any }) => h('button', `Edit ${row.id.value}`))
      const mappedHeaders: TableHeader[] = [
        { key: 'id', name: 'ID' },
        { key: 'name', name: 'Name', mapper: (value: string) => value.toUpperCase() },
      ]
      queryFunction.mockResolvedValueOnce(makePage(makeItems(1)))

      const wrapper = await mountTable({ headers: mappedHeaders, hasActions: true }, { actions })

      expect(wrapper.text()).toContain('Edit 1')
      expect(actions).toHaveBeenCalledWith(
        expect.objectContaining({
          row: expect.objectContaining({
            id: { value: 1, display: 1 },
            name: { value: 'user-1', display: 'USER-1' },
          }),
        })
      )
    })
  })
})
