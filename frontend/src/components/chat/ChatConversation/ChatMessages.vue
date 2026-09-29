<script setup>

</script>

<template>
    <div id="messages">
        <div> Message 1</div>
        <div> Message 2</div>
        <div> Message 3</div>
    </div>
</template>

<style>
#messages {
    overflow-y: scroll;
    padding: 1rem;
    display: flex;
    flex-direction: column-reverse;
}
</style>