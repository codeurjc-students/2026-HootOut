<script setup lang="ts">
  import { computed, onMounted, ref } from 'vue'
  import type { PageRequest, PageResult, TableHeader, TableItem } from '@/types'

  const { headers, queryFunction } = defineProps<{
    headers: TableHeader[]
    queryFunction: Function
  }>()

  let items = ref<TableItem[]>([])

  const sortBy = ref<string>()
  const descending = ref<boolean>(true)
  const hasNext = ref<boolean>(false)

  let pages: (string | null)[] = [null]
  const currentPage = ref<number>(0)
  const showPage = computed(() => currentPage.value + 1)
  const totalPages = ref<number>(1)

  const tableRows = computed(() => {
    let rows: TableItem[] = []
    for (let item of items.value) {
      const row: any = {}
      for (let header of headers) {
        row[header.key as keyof TableItem] = item[header.key as keyof TableItem]
      }
      rows.push(row)
    }
    return rows
  })

  async function getPage(dir: number): Promise<void> {
    currentPage.value += dir

    const nextPageRequest: PageRequest = {
      next: pages[currentPage.value],
      descending: descending.value,
      pageSize: 10,
      sortBy: sortBy.value,
    }

    const page: PageResult<unknown> = await queryFunction(nextPageRequest)
    if (!page) return

    items.value = page.items as TableItem[]
    hasNext.value = !!page.next

    totalPages.value = Math.floor(page.total / page.pageSize) + 1

    if (hasNext.value) {
      if (currentPage.value >= pages.length - 1) {
        pages.push(page.next ?? null)
      } else {
        pages[currentPage.value] = page.next ?? null
      }
    }
  }

  async function getNextPage() {
    await getPage(+1)
  }

  async function getBeforePage() {
    await getPage(-1)
  }

  async function sortTable(column: string) {
    descending.value = column !== sortBy.value ? true : !descending.value
    sortBy.value = column
    pages = [null]
    currentPage.value = 0
    await getPage(0)
  }

  onMounted(async () => {
    currentPage.value = 0
    sortBy.value = (headers.filter((h) => h.default).at(0) ?? headers[0])?.key
    await getPage(0)
  })
</script>

<template>
  <div
    class="flex h-full w-full flex-col justify-between overflow-x-auto overflow-y-auto rounded-xl border-1 background-primary p-4"
  >
    <table class="w-full table-fixed text-center">
      <thead>
        <tr class="border-bottom-1">
          <th
            v-for="header in headers"
            class="py-2"
            :class="header.canSort ? 'pointer' : ''"
            @click="
              async () => {
                if (header.canSort) await sortTable(header.key.toLocaleLowerCase())
              }
            "
          >
            <div class="align-items flex justify-center">
              <span class="inline-block">
                {{ header.name }}
              </span>
              <span
                class="material-symbols-sharp inline-block"
                v-if="sortBy == header.key.toLocaleLowerCase() && header.canSort"
              >
                {{ descending ? 'keyboard_arrow_up' : 'keyboard_arrow_down' }}
              </span>
            </div>
          </th>
        </tr>
      </thead>
      <tbody class="overflow-y-auto">
        <tr v-for="row in tableRows" class="border-bottom-1 h-4 hover:background-light">
          <td v-for="header in headers" class="py-2">
            {{ row[header.key as keyof TableItem] }}
          </td>
        </tr>
      </tbody>
    </table>
    <div class="gap-4c flex w-full justify-end">
      <div
        v-if="showPage != 1"
        class="inline-btn pointer material-symbols-sharp w-8 text-center"
        @click="getBeforePage()"
      >
        chevron_left
      </div>
      <div v-else class="w-8"></div>
      <div>Page {{ showPage }} / {{ totalPages }}</div>
      <div
        v-if="hasNext"
        class="inline-btn pointer material-symbols-sharp w-8 text-center"
        @click="getNextPage()"
      >
        chevron_right
      </div>
      <div v-else class="w-8"></div>
    </div>
  </div>
</template>
