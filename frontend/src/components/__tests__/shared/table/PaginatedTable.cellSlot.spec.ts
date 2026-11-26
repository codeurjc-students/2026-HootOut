import { describe, it, expect, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
import type { PageResult, TableHeader } from '@/types'

type Row = { id: string; username: string; userId: string; createdAt: string }

const headers: TableHeader[] = [
  { key: 'username', name: 'Username', canSort: true, default: true },
  { key: 'createdAt', name: 'Joined At', mapper: (value: string) => `joined ${value}` },
]

function page(items: Row[]): PageResult<Row> {
  return { items, next: null, total: items.length, pageSize: 10 }
}

const rows: Row[] = [{ id: 'm1', username: 'user-1', userId: 'u1', createdAt: 'today' }]

async function mountTable(slots: Record<string, (props: { row: any }) => unknown> = {}) {
  const wrapper = mount(PaginatedTable, {
    props: { headers, queryFunction: vi.fn().mockResolvedValue(page(rows)) },
    slots: slots as never,
  })
  await flushPromises()
  return wrapper
}

const cells = (wrapper: Awaited<ReturnType<typeof mountTable>>) =>
  wrapper.findAll('tbody tr')[0]!.findAll('td')

describe('PaginatedTable.vue cell slots', () => {
  it('shows the display value when the cell has no slot', async () => {
    const wrapper = await mountTable()

    expect(cells(wrapper).map((td) => td.text())).toEqual(['user-1', 'joined today'])
  })

  it('replaces the content of the cell of the slot with the row', async () => {
    const wrapper = await mountTable({
      'cell-username': ({ row }) => h('b', `${row.username.display} (${row.userId})`),
    })

    expect(cells(wrapper)[0]!.find('b').text()).toBe('user-1 (u1)')
    // The other cells keep their value
    expect(cells(wrapper)[1]!.text()).toBe('joined today')
  })

  it('passes the value and the display of the mapped columns', async () => {
    const wrapper = await mountTable({
      'cell-createdAt': ({ row }) => `${row.createdAt.value} / ${row.createdAt.display}`,
    })

    expect(cells(wrapper)[1]!.text()).toBe('today / joined today')
  })

  it('keeps the data-label of the cell for the mobile layout', async () => {
    const wrapper = await mountTable({ 'cell-username': () => 'custom' })

    expect(cells(wrapper)[0]!.attributes('data-label')).toBe('Username')
  })
})
