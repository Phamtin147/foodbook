-- WARNING: This schema is for context only and is not meant to be run.
-- Table order and constraints may not be valid for execution.

CREATE TABLE public.Comment (
  comment_id integer GENERATED ALWAYS AS IDENTITY NOT NULL,
  user_id integer NOT NULL,
  recipe_id integer NOT NULL,
  body text,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT Comment_pkey PRIMARY KEY (comment_id),
  CONSTRAINT fk_comment_user FOREIGN KEY (user_id) REFERENCES public.User(user_id),
  CONSTRAINT fk_comment_recipe FOREIGN KEY (recipe_id) REFERENCES public.Recipe(recipe_id)
);
CREATE TABLE public.EmailTokens (
  id integer NOT NULL DEFAULT nextval('"EmailTokens_id_seq"'::regclass),
  email character varying NOT NULL,
  token character varying NOT NULL,
  token_type character varying NOT NULL,
  expires_at timestamp without time zone NOT NULL,
  used boolean DEFAULT false,
  created_at timestamp without time zone DEFAULT now(),
  CONSTRAINT EmailTokens_pkey PRIMARY KEY (id)
);
CREATE TABLE public.Follow (
  follower_id integer NOT NULL,
  following_id integer NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT Follow_pkey PRIMARY KEY (follower_id, following_id),
  CONSTRAINT fk_follow_follower FOREIGN KEY (follower_id) REFERENCES public.User(user_id),
  CONSTRAINT fk_follow_following FOREIGN KEY (following_id) REFERENCES public.User(user_id)
);
CREATE TABLE public.Ingredient_Master (
  ingredient_id integer GENERATED ALWAYS AS IDENTITY NOT NULL,
  name character varying NOT NULL UNIQUE,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT Ingredient_Master_pkey PRIMARY KEY (ingredient_id)
);
CREATE TABLE public.Media (
  media_id integer GENERATED ALWAYS AS IDENTITY NOT NULL,
  media_img text,
  media_video text,
  CONSTRAINT Media_pkey PRIMARY KEY (media_id)
);
CREATE TABLE public.Notebook (
  user_id integer NOT NULL,
  recipe_id integer NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT Notebook_pkey PRIMARY KEY (user_id, recipe_id),
  CONSTRAINT fk_notebook_user FOREIGN KEY (user_id) REFERENCES public.User(user_id),
  CONSTRAINT fk_notebook_recipe FOREIGN KEY (recipe_id) REFERENCES public.Recipe(recipe_id)
);
CREATE TABLE public.Recipe (
  recipe_id integer GENERATED ALWAYS AS IDENTITY NOT NULL,
  user_id integer NOT NULL,
  name character varying,
  thumbnail_img text,
  step_number integer,
  description character varying,
  cook_time integer,
  created_at timestamp with time zone DEFAULT now(),
  level USER-DEFINED DEFAULT 'dễ'::recipe_level,
  status USER-DEFINED DEFAULT 'active'::recipe_status,
  CONSTRAINT Recipe_pkey PRIMARY KEY (recipe_id),
  CONSTRAINT fk_recipe_user FOREIGN KEY (user_id) REFERENCES public.User(user_id)
);
CREATE TABLE public.RecipeStep (
  recipe_id integer NOT NULL,
  instruction text NOT NULL,
  step integer NOT NULL,
  CONSTRAINT RecipeStep_pkey PRIMARY KEY (recipe_id, step),
  CONSTRAINT fk_recipestep_recipe FOREIGN KEY (recipe_id) REFERENCES public.Recipe(recipe_id)
);
CREATE TABLE public.RecipeStep_Media (
  recipe_id integer NOT NULL,
  step integer NOT NULL,
  media_id integer NOT NULL,
  display_order integer DEFAULT 1,
  CONSTRAINT RecipeStep_Media_pkey PRIMARY KEY (recipe_id, step, media_id),
  CONSTRAINT fk_rsm_recipestep FOREIGN KEY (recipe_id) REFERENCES public.RecipeStep(recipe_id),
  CONSTRAINT fk_rsm_recipestep FOREIGN KEY (recipe_id) REFERENCES public.RecipeStep(step),
  CONSTRAINT fk_rsm_recipestep FOREIGN KEY (step) REFERENCES public.RecipeStep(recipe_id),
  CONSTRAINT fk_rsm_recipestep FOREIGN KEY (step) REFERENCES public.RecipeStep(step),
  CONSTRAINT fk_rsm_media FOREIGN KEY (media_id) REFERENCES public.Media(media_id)
);
CREATE TABLE public.Recipe_Ingredient (
  recipe_id integer NOT NULL,
  ingredient_id integer NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT Recipe_Ingredient_pkey PRIMARY KEY (recipe_id, ingredient_id),
  CONSTRAINT fk_ri_recipe FOREIGN KEY (recipe_id) REFERENCES public.Recipe(recipe_id),
  CONSTRAINT fk_ri_ingredient FOREIGN KEY (ingredient_id) REFERENCES public.Ingredient_Master(ingredient_id)
);
CREATE TABLE public.Recipe_RecipeType (
  recipe_id integer NOT NULL,
  recipe_type_id integer NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT Recipe_RecipeType_pkey PRIMARY KEY (recipe_id, recipe_type_id),
  CONSTRAINT fk_rrt_recipe FOREIGN KEY (recipe_id) REFERENCES public.Recipe(recipe_id),
  CONSTRAINT fk_rrt_recipe_type FOREIGN KEY (recipe_type_id) REFERENCES public.Recipe_type(recipe_type_id)
);
CREATE TABLE public.Recipe_type (
  recipe_type_id integer GENERATED ALWAYS AS IDENTITY NOT NULL,
  content character varying,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT Recipe_type_pkey PRIMARY KEY (recipe_type_id)
);
CREATE TABLE public.Report (
  user_id integer NOT NULL,
  recipe_id integer NOT NULL,
  body text,
  status USER-DEFINED NOT NULL DEFAULT 'Đang xử lý'::report_status,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT Report_pkey PRIMARY KEY (user_id, recipe_id),
  CONSTRAINT fk_report_user FOREIGN KEY (user_id) REFERENCES public.User(user_id),
  CONSTRAINT fk_report_recipe FOREIGN KEY (recipe_id) REFERENCES public.Recipe(recipe_id)
);
CREATE TABLE public.Share (
  user_id integer NOT NULL,
  recipe_id integer NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT Share_pkey PRIMARY KEY (user_id, recipe_id),
  CONSTRAINT fk_share_user FOREIGN KEY (user_id) REFERENCES public.User(user_id),
  CONSTRAINT fk_share_recipe FOREIGN KEY (recipe_id) REFERENCES public.Recipe(recipe_id)
);
CREATE TABLE public.User (
  user_id integer GENERATED ALWAYS AS IDENTITY NOT NULL,
  username character varying NOT NULL UNIQUE,
  full_name character varying,
  email character varying NOT NULL UNIQUE,
  password character varying NOT NULL,
  avatar_img text,
  bio text,
  created_at timestamp with time zone DEFAULT now(),
  status USER-DEFINED DEFAULT 'active'::user_status,
  role USER-DEFINED NOT NULL DEFAULT 'user'::user_role,
  is_verified boolean DEFAULT false,
  CONSTRAINT User_pkey PRIMARY KEY (user_id)
);
CREATE TABLE public.User-Trigger (
  username character varying NOT NULL,
  full_name character varying,
  email character varying NOT NULL,
  password character varying NOT NULL,
  avatar_img bytea,
  bio text,
  created_at timestamp with time zone DEFAULT now(),
  status USER-DEFINED DEFAULT 'active'::user_status,
  is_verified boolean,
  role USER-DEFINED
);
CREATE TABLE public.User_Report (
  reporter_id integer NOT NULL,
  reported_user_id integer NOT NULL,
  body text,
  status text NOT NULL DEFAULT 'Đang xử lý'::text,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT User_Report_pkey PRIMARY KEY (reporter_id, reported_user_id),
  CONSTRAINT fk_reporter FOREIGN KEY (reporter_id) REFERENCES public.User(user_id),
  CONSTRAINT fk_reported_user FOREIGN KEY (reported_user_id) REFERENCES public.User(user_id)
);
CREATE TABLE public.like_dislike (
  ld_id integer GENERATED ALWAYS AS IDENTITY NOT NULL,
  user_id integer NOT NULL,
  recipe_id integer NOT NULL,
  body text,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT like_dislike_pkey PRIMARY KEY (ld_id),
  CONSTRAINT fk_ld_user FOREIGN KEY (user_id) REFERENCES public.User(user_id),
  CONSTRAINT fk_ld_recipe FOREIGN KEY (recipe_id) REFERENCES public.Recipe(recipe_id)
);

-- Trigger function to activate user when verified
CREATE OR REPLACE FUNCTION activate_user_on_verification()
RETURNS TRIGGER AS $$
BEGIN
  -- When is_verified changes to true, set status to active
  IF NEW.is_verified = true AND (OLD.is_verified IS NULL OR OLD.is_verified = false) THEN
    NEW.status = 'active';
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger on User-Trigger table
CREATE TRIGGER trigger_activate_user_on_verification
BEFORE UPDATE ON public."User-Trigger"
FOR EACH ROW
EXECUTE FUNCTION activate_user_on_verification();