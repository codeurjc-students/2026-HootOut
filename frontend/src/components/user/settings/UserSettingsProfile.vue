<script setup lang="ts">
  import RoundedProfilePicture from '@/components/shared/filePreview/RoundedProfilePicture.vue'
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import { dateTimeMapper, getValidationErrors, isValidPicture } from '@/services/common/utils'
  import MediaFileService from '@/services/mediaFile/MediaFileService'
  import UserService from '@/services/users/UserService'
  import { userInfoStore } from '@/stores/userInfo'
  import { ApiError, ValidationError, type UploadFileResponse } from '@/types'
  import { storeToRefs } from 'pinia'
  import { computed, reactive, ref } from 'vue'

  const userInfo = userInfoStore()
  const { user } = storeToRefs(userInfo)

  const form = reactive({ profilePictureId: '' })
  const submitting = ref(false)
  const error = ref('')

  const formErrors = ref(new Map<string, Map<string, string>>())

  const fileInput = ref<HTMLInputElement>()
  const uploading = ref(false)
  const profilePicture = ref<UploadFileResponse | undefined>()

  const profilePictureUrl = computed(() =>
    profilePicture.value ? profilePicture.value : user.value.profilePicture
  )

  const canSubmit = computed(
    () => !submitting.value && form.profilePictureId && form.profilePictureId.trim() !== ''
  )

  async function onSubmit() {
    if (!canSubmit.value) return

    error.value = ''
    formErrors.value = new Map()
    submitting.value = true

    try {
      const picture = await UserService.updateProfilePicture({
        mediaFileId: form.profilePictureId,
      })
      user.value.profilePicture = picture
    } catch (err) {
      error.value =
        err instanceof ApiError && err.status < 500
          ? err.message
          : 'Something went wrong. Please try again.'

      if (err instanceof ValidationError && err.status == 400) {
        error.value = 'Please review the form.'
        formErrors.value = getValidationErrors(err)
      }
    } finally {
      submitting.value = false
      cleanForm()
    }
  }

  async function uploadFile() {
    const input = fileInput.value
    const file = input?.files?.item(0)
    if (!input || !file) return
    uploading.value = true
    try {
      const response = await MediaFileService.uploadFile(file)
      if (response) {
        if (!isValidPicture(response.contentType)) {
          const err = new Map<string, string>()
          err.set('error', 'This file type is not valid for the server profile picture')
          formErrors.value.set('ProfilePicture', err)
        } else {
          profilePicture.value = response
          form.profilePictureId = response.id
        }
      }
    } catch (err) {
      // TODO: show the error to the user
    } finally {
      uploading.value = false
      // Lets the same file be selected again
      input.value = ''
    }
  }

  function selectFile() {
    fileInput.value?.click()
  }

  async function cleanForm() {
    formErrors.value = new Map()
    submitting.value = false
    profilePicture.value = undefined
  }
</script>

<template>
  <form class="p-4" novalidate @submit.prevent="onSubmit">
    <GoBackLink class="text-sm font-semibold" :to="{ name: 'userSettings' }">
      User Profile:
    </GoBackLink>
    <h1 class="mt-2 ml-2 hidden text-2xl font-semibold md:block">User Profile:</h1>
    <div class="flex flex-col px-4 py-2 text-lg md:p-4" v-if="user">
      <div class="flex flex-col py-2 border-bottom-1">
        <div class="font-semibold">User Profile Picture:</div>
        <RoundedProfilePicture
          class="pointer material-symbols-sharp m-4 h-40 w-40 text-[10rem]! max-sm:mx-auto"
          :img-url="profilePictureUrl?.url"
          alt="account_circle"
          v-tooltip:right="user.username"
        ></RoundedProfilePicture>

        <button
          type="button"
          @submit=""
          class="pointer button-action w-fit py-1!"
          @click="async () => await selectFile()"
        >
          Change Profile picture
        </button>
        <input id="file-input" ref="fileInput" type="file" hidden @change="uploadFile" />
      </div>

      <div class="pb-2 border-bottom-1">
        <div class="font-semibold">Username:</div>
        <div>{{ user.username }}</div>
      </div>
      <div class="py-2 border-bottom-1">
        <div class="font-semibold">Email:</div>
        <div>{{ user.email }}</div>
      </div>
      <div class="py-2 border-bottom-1" v-if="user.createdAt">
        <div class="font-semibold">Account Created:</div>
        <div>{{ dateTimeMapper(user.createdAt) }}</div>
      </div>

      <div>
        <p
          v-for="err in formErrors.get('Description')?.values()"
          :key="err"
          class="custom-form-field-error"
        >
          {{ err }}
        </p>
      </div>

      <div class="flex flex-col md:flex-row md:gap-4">
        <button
          class="button-action button-action-success mt-4 md:w-fit"
          type="submit"
          :disabled="submitting || !form.profilePictureId"
          v-if="profilePicture"
        >
          Update Profile
        </button>

        <button
          class="button-action button-action-danger mt-4 md:w-fit"
          type="button"
          :disabled="submitting || !form.profilePictureId"
          @click="cleanForm"
          @submit=""
          v-if="profilePicture"
        >
          Revert Changes
        </button>
      </div>
    </div>
  </form>
</template>
