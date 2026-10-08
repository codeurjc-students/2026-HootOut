<script setup lang="ts">
</script>

<template>
    <section id="register">
        <div class="custom-form-wrapper">
            <h1> Create Account </h1>
            <form class="custom-form" id="register-form">
                <div class="custom-form-row">
                    <div class="custom-form-label-wrapper">
                        <label for="username">Username</label>
                    </div>
                    <div class="custom-form-input-wrapper">
                        <input class="custom-form-text-input" name="username" type="text" />
                    </div>
                </div>
                <div class="custom-form-row">
                    <div class="custom-form-label-wrapper">
                        <label for="email">Email</label>
                    </div>
                    <div class="custom-form-input-wrapper">
                        <input class="custom-form-text-input" name="email" type="text" />
                    </div>
                </div>
                <div class="custom-form-row">
                    <div class="custom-form-label-wrapper">
                        <label for="password">Password</label>
                    </div>
                    <div class="custom-form-input-wrapper">
                        <input class="custom-form-text-input" name="password" type="password" />
                    </div>
                </div>

                <button class="button-home" type="button"> Create Account</button>
            </form>
            <div class="custom-form-link">
                <RouterLink to="/login">Log In</RouterLink>
            </div>
        </div>
    </section>
</template>

<style>
#register {
    background-color: var(--color-background-soft);
    width: 100%;
    height: 100%;
    padding-top: 5rem;
}

@media(max-width: 520px) {
    #register {
        padding: 0;
    }
}
</style>